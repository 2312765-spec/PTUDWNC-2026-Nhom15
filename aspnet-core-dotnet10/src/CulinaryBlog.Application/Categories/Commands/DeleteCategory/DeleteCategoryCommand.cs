using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Categories.Commands.DeleteCategory;

/// <summary>
/// Command xóa danh mục bởi Quản trị viên (SRS FR-CAT-005)
/// Quy tắc: KHÔNG được xóa danh mục còn chứa công thức (dù là Published hay Draft).
/// </summary>
public record DeleteCategoryCommand(Guid Id) : IRequest<Unit>;

public class DeleteCategoryCommandValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("ID danh mục cần xóa không được để trống.");
    }
}

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Unit>
{
    private const string CacheKey = "categories:all";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteCategoryCommandHandler> _logger;

    public DeleteCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService,
        ILogger<DeleteCategoryCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        DeleteCategoryCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra role Admin (SRS FR-CAT-005 Luồng chính bước 2 & Ngoại lệ A2: HTTP 403 Forbidden)
        if (!_currentUserService.IsAdmin)
        {
            _logger.LogWarning("Access Denied: User '{UserId}' lacks Admin role to delete category '{CategoryId}'.", _currentUserService.UserId, request.Id);
            throw new ForbiddenException("Chỉ Quản trị viên (Admin) mới có quyền xóa danh mục.");
        }

        // 2. Kiểm tra tồn tại category theo ID (SRS FR-CAT-005 Ngoại lệ A2 – ID không tồn tại: HTTP 404 Not Found)
        var category = await _unitOfWork.Categories.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            _logger.LogWarning("NotFound: Category with ID '{CategoryId}' does not exist.", request.Id);
            throw new NotFoundException("Category", request.Id);
        }

        // 3. Quy tắc nghiệp vụ (SRS FR-CAT-005 Luồng chính bước 4 & Ngoại lệ A1):
        // Đếm số recipe trong category (cả Published lẫn Draft/Archived): nếu > 0 → Throw ConflictException
        var recipeCount = await _unitOfWork.Categories.CountRecipesAsync(category.Id, cancellationToken);
        if (recipeCount > 0)
        {
            _logger.LogWarning("Conflict: Category '{CategoryId}' still contains {Count} recipes. Cannot delete.", request.Id, recipeCount);
            throw new ConflictException($"Danh mục còn chứa {recipeCount} công thức. Vui lòng chuyển hoặc xóa tất cả công thức trước khi xóa danh mục.");
        }

        // 4. Xóa entity (SRS FR-CAT-005 Luồng chính bước 5)
        await _unitOfWork.Categories.DeleteAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Invalidate cache "categories:all"
        _memoryCache.Remove(CacheKey);
        _logger.LogInformation("Category '{CategoryId}' ('{Name}') was deleted. Cache key '{CacheKey}' was invalidated.", request.Id, category.Name, CacheKey);

        // 6. Trả về Unit để WebApi map ra HTTP 204 No Content
        return Unit.Value;
    }
}
