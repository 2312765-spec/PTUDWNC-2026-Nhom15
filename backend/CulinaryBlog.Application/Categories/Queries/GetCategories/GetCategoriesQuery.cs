using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Categories.Queries.GetCategories;

/// <summary>
/// Query lấy danh sách tất cả danh mục (SRS FR-CAT-001)
/// </summary>
public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private const string CacheKey = "categories:all";
    private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(60);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GetCategoriesQueryHandler> _logger;

    public GetCategoriesQueryHandler(
        IUnitOfWork _unitOfWork,
        IMemoryCache memoryCache,
        ILogger<GetCategoriesQueryHandler> logger)
    {
        this._unitOfWork = _unitOfWork;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(
        GetCategoriesQuery request, 
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra IMemoryCache với key "categories:all" (SRS FR-CAT-001 Luồng chính bước 3)
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<CategoryDto>? cachedCategories) && cachedCategories is not null)
        {
            _logger.LogInformation("Cache HIT for key '{CacheKey}'. Returning {Count} categories.", CacheKey, cachedCategories.Count);
            return cachedCategories;
        }

        // 2. Cache miss: query database (_unitOfWork.Categories.GetAllWithRecipeCount())
        _logger.LogInformation("Cache MISS for key '{CacheKey}'. Querying database via UnitOfWork.", CacheKey);
        var categoriesWithCount = await _unitOfWork.Categories.GetAllWithRecipeCountAsync(cancellationToken);

        // 3. Map sang CategoryDto[] và sắp xếp theo Name tăng dần (A-Z)
        var categoryDtos = categoriesWithCount
            .Select(c => new CategoryDto(
                c.Category.Id,
                c.Category.Name,
                c.Category.Slug,
                c.Category.Description,
                c.Category.ImageUrl,
                c.PublishedRecipeCount,
                c.Category.OrderIndex
            ))
            .OrderBy(c => c.Name, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), ignoreCase: true))
            .ToList();

        // 4. Lưu vào IMemoryCache với TTL 60 phút (sliding expiration) (SRS Luồng chính bước 6)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheSlidingExpiration,
            Priority = CacheItemPriority.High
        };

        _memoryCache.Set(CacheKey, (IReadOnlyList<CategoryDto>)categoryDtos, cacheOptions);

        _logger.LogInformation("Successfully cached {Count} categories with key '{CacheKey}' and sliding expiration of 60 mins.", categoryDtos.Count, CacheKey);

        return categoryDtos;
    }
}
