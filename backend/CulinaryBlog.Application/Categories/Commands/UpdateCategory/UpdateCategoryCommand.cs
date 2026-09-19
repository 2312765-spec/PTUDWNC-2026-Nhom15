using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

/// <summary>
/// Command cập nhật thông tin danh mục bởi Quản trị viên (SRS FR-CAT-004)
/// Quy tắc: Admin cập nhật Name và/hoặc Description. Slug KHÔNG thay đổi khi đổi tên (để tránh broken links).
/// </summary>
public record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description = null,
    string? ImageUrl = null,
    int OrderIndex = 0
) : IRequest<CategoryDto>;

/// <summary>
/// FluentValidation xác thực dữ liệu: Name 2-50 ký tự, không chứa thẻ HTML (SRS FR-CAT-004)
/// </summary>
public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID danh mục không được để trống.");

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

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private const string CacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateCategoryCommandHandler> _logger;

    public UpdateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService,
        ILogger<UpdateCategoryCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<CategoryDto> Handle(
        UpdateCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra role Admin (SRS FR-CAT-004 A2 – Thiếu role Admin: HTTP 403 Forbidden)
        if (!_currentUserService.IsAdmin)
        {
            _logger.LogWarning("Access Denied: User '{UserId}' lacks Admin role to update category '{CategoryId}'.", _currentUserService.UserId, request.Id);
            throw new ForbiddenException("Chỉ Quản trị viên (Admin) mới có quyền cập nhật danh mục.");
        }

        // 2. Tìm category theo ID (SRS FR-CAT-004 A1 – ID không tồn tại: HTTP 404 Not Found)
        var category = await _unitOfWork.Categories.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            _logger.LogWarning("NotFound: Category with ID '{CategoryId}' does not exist.", request.Id);
            throw new NotFoundException("Category", request.Id);
        }

        var trimmedName = request.Name.Trim();

        // 3. Kiểm tra trùng lặp tên với danh mục khác (409 Conflict)
        var isDuplicateName = await _unitOfWork.Categories.ExistsByNameExcludingIdAsync(trimmedName, request.Id, cancellationToken);
        if (isDuplicateName)
        {
            _logger.LogWarning("Conflict: Another category already uses name '{Name}'.", trimmedName);
            throw new ConflictException($"Tên danh mục '{trimmedName}' đã được sử dụng bởi danh mục khác.");
        }

        // 4. Cập nhật Entity qua domain method:
        // ĐẶC BIỆT LƯU Ý: Slug KHÔNG thay đổi khi đổi tên để tránh gãy liên kết SEO và bookmarks!
        category.Update(
            trimmedName,
            request.Description,
            request.ImageUrl,
            request.OrderIndex
        );

        // 5. Lưu thay đổi vào database
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Invalidate cache IMemoryCache "categories:all"
        _memoryCache.Remove(CacheKey);
        _logger.LogInformation("Category '{CategoryId}' updated successfully. Cache key '{CacheKey}' was invalidated.", request.Id, CacheKey);

        // 7. Đếm số công thức để trả về Dto đầy đủ
        var recipeCount = await _unitOfWork.Categories.CountRecipesAsync(category.Id, cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug, // Giữ nguyên slug ban đầu
            category.Description,
            category.ImageUrl,
            recipeCount,
            category.OrderIndex
        );
    }
}
