using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Tên danh mục không được để trống.", nameof(request.Name));
        }

        var trimmedName = request.Name.Trim();

        // 1. Kiểm tra trùng tên danh mục (Yêu cầu mã lỗi: CATEGORY_NAME_EXISTS)
        var exists = await _categoryRepository.ExistsByNameAsync(trimmedName, cancellationToken);
        if (exists)
        {
            throw new ConflictException("CATEGORY_NAME_EXISTS", $"Danh mục với tên '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        // 2. Tạo Slug duy nhất (Quyết định D10: Bắt đầu thêm hậu tố từ -2)
        var baseSlug = GenerateSlug(trimmedName);
        var uniqueSlug = baseSlug;
        var counter = 2;

        while (await _categoryRepository.ExistsBySlugAsync(uniqueSlug, cancellationToken))
        {
            uniqueSlug = $"{baseSlug}-{counter++}";
        }

        // 3. Khởi tạo thực thể Category (Domain Aggregate)
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

    private static string GenerateSlug(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var cleanText = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        cleanText = cleanText.Replace("đ", "d").Replace("Đ", "D");
        cleanText = Regex.Replace(cleanText.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        cleanText = Regex.Replace(cleanText, @"\s+", "-").Trim('-');

        return string.IsNullOrWhiteSpace(cleanText) ? "danh-muc" : cleanText;
    }
}