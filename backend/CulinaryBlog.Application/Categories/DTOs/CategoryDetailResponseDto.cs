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
    DateTime CreatedAt,
    // FR-SRCH-001: điểm ts_rank — chỉ có ở kết quả tìm kiếm, danh sách thường để null.
    double? RelevanceScore = null,
    // FR-JOB-002/D88: ảnh cắt đúng khung card (760×320) của cùng ảnh đại diện; null khi job chưa tạo. FeaturedImageUrl
    // vẫn là ảnh gốc (dùng cho chỗ cần ảnh đầy đủ).
    string? FeaturedImageCardUrl = null
);

public record CategoryDetailResponseDto(
    CategoryDto Category,
    PagedResult<RecipeSummaryDto> Recipes
);