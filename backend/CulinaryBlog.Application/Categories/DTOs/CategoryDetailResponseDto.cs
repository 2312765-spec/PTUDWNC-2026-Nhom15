using System;
using CulinaryBlog.Application.Common.Models;

namespace CulinaryBlog.Application.Categories.DTOs;

public record RecipeSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string? FeaturedImageUrl,
    string Status,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    string Difficulty,
    Guid AuthorId,
    string? AuthorName,
    DateTime CreatedAt
);

public record CategoryDetailResponseDto(
    CategoryDto Category,
    PagedResult<RecipeSummaryDto> Recipes
);