using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategories;

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
        // Gọi đúng tên hàm GetAllWithRecipesAsync trong ICategoryRepository của nhóm
        var categories = await _categoryRepository.GetAllWithRecipesAsync(cancellationToken);

        // Map sang CategoryDto và đếm các công thức Published
        return categories
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.Recipes != null 
                    ? c.Recipes.Count(r => r.Status == RecipeStatus.Published && !r.IsDeleted) 
                    : 0
            ))
            .ToList();
    }
}