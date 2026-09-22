using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISlugHelper _slugHelper;

    public CreateCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ISlugHelper slugHelper)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _slugHelper = slugHelper;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var nameTrimmed = request.Name.Trim();

        // 1. Kiểm tra Name trùng lặp -> 409 Conflict
        if (await _categoryRepository.ExistsByNameAsync(nameTrimmed, cancellationToken))
        {
            throw new ConflictException("CATEGORY_NAME_EXISTS", $"Danh mục với tên '{nameTrimmed}' đã tồn tại.");
        }

        // 2. D10: Sinh slug tự động bằng ISlugHelper
        var baseSlug = _slugHelper.Generate(nameTrimmed);
        var finalSlug = baseSlug;
        var suffix = 2;

        // Nếu trùng slug thì thêm suffix -2, -3... không bao giờ ném lỗi (D10)
        while (await _categoryRepository.ExistsBySlugAsync(finalSlug, cancellationToken))
        {
            finalSlug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        // 3. Tạo Entity Category qua Factory Method của Domain
        var category = Category.Create(nameTrimmed, finalSlug, request.Description?.Trim(), null, 0);
        // 4. Thêm vào DB và SaveChanges
        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Cache invalidation tự động được xử lý bởi CacheInvalidationBehavior qua ICacheInvalidator

        // 6. Trả về CategoryDto 5 trường chuẩn của dự án bạn
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            0
        );
    }
}