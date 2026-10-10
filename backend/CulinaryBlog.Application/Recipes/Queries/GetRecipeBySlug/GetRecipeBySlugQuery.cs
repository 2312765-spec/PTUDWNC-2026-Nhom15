using CulinaryBlog.Application.Recipes.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Recipes.Queries.GetRecipeBySlug;

/// <summary>
/// FR-RCP-002 — chi tiết công thức theo slug. KHÔNG cache ở tầng này: kiểm tra quyền xem
/// Draft/Archived (A2 → 403) phải chạy ở MỌI request, kể cả khi dữ liệu lấy từ cache của
/// <see cref="GetRecipeDetailQuery"/>.
/// </summary>
public sealed record GetRecipeBySlugQuery(string Slug) : IRequest<RecipeDetailDto>;
