using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

/// <summary>
/// Command tạo danh mục mới bởi Quản trị viên (SRS FR-CAT-003)
/// </summary>
public record CreateCategoryCommand(
    string Name,
    string? Description = null,
    string? ImageUrl = null,
    int OrderIndex = 0
) : IRequest<CategoryDto>;

/// <summary>
/// FluentValidation xác thực dữ liệu: Name 2-50 ký tự, không chứa thẻ HTML (SRS FR-CAT-003 Luồng chính bước 4)
/// </summary>
public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MinimumLength(2).WithMessage("Tên danh mục phải có tối thiểu 2 ký tự.")
            .MaximumLength(50).WithMessage("Tên danh mục không được vượt quá 50 ký tự.")
            .Matches(@"^[^<>]+$").WithMessage("Tên danh mục không được chứa các thẻ hoặc mã HTML.");

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả không được vượt quá 500 ký tự.")
                .Matches(@"^[^<>]+$").WithMessage("Mô tả không được chứa các thẻ hoặc mã HTML.");
        });

        When(x => !string.IsNullOrEmpty(x.ImageUrl), () =>
        {
            RuleFor(x => x.ImageUrl)
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
                .WithMessage("Đường dẫn hình ảnh phải là URL hợp lệ.");
        });
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private const string CacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateCategoryCommandHandler> _logger;

    public CreateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService,
        ILogger<CreateCategoryCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CategoryDto> Handle(
        CreateCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra quyền Admin (SRS FR-CAT-003 Ngoại lệ A1 – Thiếu role Admin: HTTP 403 Forbidden)
        if (!_currentUserService.IsAdmin)
        {
            _logger.LogWarning("Access Denied: User '{UserId}' lacks Admin role to create category.", _currentUserService.UserId);
            throw new ForbiddenException("Chỉ Quản trị viên (Admin) mới có quyền tạo danh mục công thức mới.");
        }

        var trimmedName = request.Name.Trim();

        // 2. Kiểm tra Name chưa tồn tại trong database (SRS Điều kiện tiên quyết 2 & Ngoại lệ 409 Conflict)
        var existsName = await _unitOfWork.Categories.ExistsByNameAsync(trimmedName, cancellationToken);
        if (existsName)
        {
            _logger.LogWarning("Conflict: Category with name '{Name}' already exists.", trimmedName);
            throw new ConflictException($"Tên danh mục '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        // 3. SlugHelper.Generate(name) tạo slug (SRS Luồng chính bước 5)
        var baseSlug = SlugHelper.Generate(trimmedName);
        var candidateSlug = baseSlug;
        var suffix = 2;

        // 4. Kiểm tra slug chưa tồn tại. Nếu trùng, thêm "-2", "-3",... cho đến khi unique (SRS Luồng chính bước 6)
        while (await _unitOfWork.Categories.ExistsBySlugAsync(candidateSlug, cancellationToken))
        {
            candidateSlug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        _logger.LogInformation("Generated unique slug '{Slug}' for category '{Name}'.", candidateSlug, trimmedName);

        // 5. Category.Create(name, slug, description) tạo entity (SRS Luồng chính bước 7)
        var category = Category.Create(
            trimmedName,
            candidateSlug,
            request.Description,
            request.ImageUrl,
            request.OrderIndex
        );

        // 6. _unitOfWork.Categories.AddAsync(entity) (SRS Luồng chính bước 8)
        await _unitOfWork.Categories.AddAsync(category, cancellationToken);

        // 7. _unitOfWork.SaveChangesAsync() (SRS Luồng chính bước 9)
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 8. MemoryCache.Remove("categories:all") – invalidate cache (SRS Luồng chính bước 10)
        _memoryCache.Remove(CacheKey);
        _logger.LogInformation("Cache key '{CacheKey}' was invalidated after new category creation.", CacheKey);

        // 9. Map sang CategoryDto
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ImageUrl,
            0,
            category.OrderIndex
        );
    }
}
