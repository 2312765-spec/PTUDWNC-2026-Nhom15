using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.DeleteCategory;

/// <summary>
/// Handler xử lý xóa danh mục (FR-CAT-005).
/// Tuân thủ Quyết định D2: Soft delete (IsDeleted = true), kiểm tra không còn recipe nào.
/// </summary>
public sealed class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeleteCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        // 0. Chỉ Admin (NFR-SEC-006: kiểm tra lại ở Application Layer, không chỉ dựa vào AdminPolicy ở endpoint)
        if (!_currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.CategoryForbidden, "Chỉ Admin mới được xóa danh mục.");
        }

        // 1. Tìm category theo Id
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(ErrorCodes.CategoryNotFound, $"Không tìm thấy danh mục với ID: '{request.Id}'.");
        }

        // 2. Đếm số lượng recipe thuộc danh mục (kể cả Draft, Published, Archived)
        var recipeCount = await _categoryRepository.GetTotalRecipeCountAsync(category.Id, cancellationToken);
        if (recipeCount > 0)
        {
            // Quyết định D2 & Bảng ErrorCodes: CATEGORY_DELETE_HAS_RECIPES -> 409 Conflict
            throw new ConflictException(
                ErrorCodes.CategoryDeleteHasRecipes, 
                $"Danh mục còn chứa {recipeCount} công thức. Vui lòng chuyển công thức sang danh mục khác trước khi xóa.");
        }

        // 3. Thực hiện Soft Delete (Quyết định D2: Tuyệt đối không xóa cứng)
        category.SoftDelete(); // hoặc category.SoftDelete(); tùy hàm trong BaseEntity của bạn

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}