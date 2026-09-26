using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

/// <summary>
/// Handler xử lý cập nhật danh mục món ăn (FR-CAT-004).
/// Tuân thủ Quyết định D10: Giữ nguyên Slug ban đầu, chỉ cập nhật Name và Description.
/// </summary>
public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService? _cacheService;

    public UpdateCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICacheService? cacheService = null)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }
    
    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tên danh mục không được để trống.", nameof(request.Name));
        }

        var trimmedName = request.Name.Trim();

        // 1. Tìm danh mục theo ID
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException("CATEGORY_NOT_FOUND", $"Không tìm thấy danh mục với ID: '{request.Id}'.");
        }

        // 2. Kiểm tra nếu tên thay đổi và trùng với danh mục khác (Yêu cầu mã: CATEGORY_NAME_EXISTS)
        if (!string.Equals(category.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _categoryRepository.ExistsByNameAsync(trimmedName, cancellationToken);
            if (exists)
            {
                throw new ConflictException("CATEGORY_NAME_EXISTS", $"Danh mục với tên '{trimmedName}' đã tồn tại trong hệ thống.");
            }
        }

        // 3. Cập nhật thông tin (Giữ nguyên Slug theo Quyết định D10)
        category.Update(trimmedName, null, request.Description?.Trim());

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Xóa cache của cả categories và recipes (thỏa mãn Command_InvalidatesCategoriesAndRecipesTags)
        if (_cacheService != null)
        {
            await _cacheService.RemoveByTagsAsync(new[] { "categories", "recipes" }, cancellationToken);
        }

        // 5. Lấy số lượng công thức liên kết
        var recipeCount = await _categoryRepository.GetPublishedRecipeCountAsync(category.Id, cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            recipeCount
        );
    }
}