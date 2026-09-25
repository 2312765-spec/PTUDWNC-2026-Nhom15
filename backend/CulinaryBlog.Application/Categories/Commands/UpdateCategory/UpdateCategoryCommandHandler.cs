using System.Threading;
using System.Threading.Tasks;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm category theo ID
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null || category.IsDeleted)
        {
            throw new NotFoundException(ErrorCodes.CategoryNotFound, $"Không tìm thấy danh mục với ID '{request.Id}'.");
        }

        var nameTrimmed = request.Name.Trim();

        // 2. Kiểm tra nếu tên thay đổi có bị trùng với danh mục khác không
        if (!string.Equals(category.Name, nameTrimmed, System.StringComparison.OrdinalIgnoreCase))
        {
            var isNameExists = await _categoryRepository.ExistsByNameAsync(nameTrimmed, cancellationToken);
            if (isNameExists)
            {
                // D4: Báo lỗi 409 Conflict với ErrorCodes chuẩn
                throw new ConflictException(ErrorCodes.CategoryNameExists, $"Tên danh mục '{nameTrimmed}' đã tồn tại.");
            }
        }

        // 3. Cập nhật Category (Slug tuyệt đối KHÔNG đổi theo D10)
        category.Update(nameTrimmed, request.Description?.Trim());

        // 4. Lưu vào CSDL
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Đếm số lượng công thức Published để trả về DTO
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