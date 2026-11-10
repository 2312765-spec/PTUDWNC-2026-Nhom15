using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategories;

/// <summary>
/// FR-CAT-001. recipeCount = số công thức Published chưa xóa, đếm trong database (NFR-PERF-004).
/// </summary>
public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllWithPublishedRecipeCountAsync(cancellationToken);

        return categories
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.Description, c.PublishedRecipeCount))
            .ToList();
    }
}
