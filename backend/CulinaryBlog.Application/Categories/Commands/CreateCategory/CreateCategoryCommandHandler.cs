using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

/// <summary>
/// Handler xử lý tạo danh mục món ăn mới (FR-CAT-003).
/// Tuân thủ Quyết định D10: Tự động sinh URL-friendly slug, xử lý hậu tố nếu trùng.
/// </summary>
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
        // NotEmpty/Length/HTML đã được CreateCategoryCommandValidator (FluentValidation +
        // ValidationBehavior, CONS-008) chặn từ trước — Name khác null/rỗng khi tới đây.
        var trimmedName = request.Name.Trim();

        // 1. Kiểm tra trùng tên danh mục (Yêu cầu mã lỗi: CATEGORY_NAME_EXISTS)
        var exists = await _categoryRepository.ExistsByNameAsync(trimmedName, cancellationToken);
        if (exists)
        {
            throw new ConflictException("CATEGORY_NAME_EXISTS", $"Danh mục với tên '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        // 2. Tạo Slug duy nhất qua ISlugHelper dùng chung (D10: auto-suffix bắt đầu từ "-2",
        // không bao giờ trả 409 — SlugHelper.GenerateUniqueAsync bắt đầu suffix từ "-1" nên
        // không dùng ở đây, tự lặp bắt đầu từ 2 để khớp D10).
        var baseSlug = _slugHelper.Generate(trimmedName);
        var uniqueSlug = baseSlug;
        var counter = 2;

        while (await _categoryRepository.ExistsBySlugAsync(uniqueSlug, cancellationToken))
        {
            uniqueSlug = $"{baseSlug}-{counter++}";
        }

        // 3. Khởi tạo thực thể Category (Domain Aggregate) — validate Name/Slug lần nữa ở Domain
        var category = Category.Create(trimmedName, uniqueSlug, request.Description?.Trim());

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Trả về DTO
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            0
        );
    }
}