using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeById;

/// <summary>
/// D75 — GET /api/v1/recipes/{id:guid}: trang sửa công thức (FR-RCP-004 FE) cần đọc theo id và luôn có
/// rowVersion mới nhất (D71), nên KHÔNG cache. Quyền xem giống FR-RCP-002.
/// </summary>
public sealed record GetRecipeByIdQuery(Guid Id) : IRequest<RecipeDetailDto>;
