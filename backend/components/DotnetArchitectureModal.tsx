import React, { useState } from 'react';
import { 
  Code2, 
  Copy, 
  Check, 
  X, 
  Layers, 
  Database, 
  ShieldCheck, 
  Sparkles, 
  FileCode2, 
  ExternalLink,
  ChevronRight,
  Terminal,
  Cpu
} from 'lucide-react';

interface DotnetArchitectureModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const codeSnippets: Record<string, { title: string; layer: string; path: string; req: string; code: string }> = {
  controller: {
    title: 'CategoriesController.cs',
    layer: 'Presentation (WebApi)',
    path: 'src/CulinaryBlog.WebApi/Controllers/CategoriesController.cs',
    req: 'FR-CAT-001, 002, 003',
    code: `using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.WebApi.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// FR-CAT-001: Xem Danh sách Tất cả Danh mục
    /// Kết quả được cache với IMemoryCache (TTL 60 phút sliding) và sắp xếp theo Name tăng dần.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// FR-CAT-002: Xem Chi tiết Danh mục và Công thức phân trang (OFFSET-based)
    /// </summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CategoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDetailDto>> GetBySlug(
        [FromRoute] string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCategoryBySlugQuery(slug, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// FR-CAT-003: Tạo Danh mục Mới [Admin]
    /// Yêu cầu JWT role Admin. Slug tự động sinh; Invalidate cache "categories:all".
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }
}`
  },

  getCategories: {
    title: 'GetCategoriesQueryHandler.cs',
    layer: 'Application (CQRS / MediatR)',
    path: 'src/CulinaryBlog.Application/Categories/Queries/GetCategories/GetCategoriesQuery.cs',
    req: 'FR-CAT-001 (IMemoryCache TTL 60m)',
    code: `using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Categories.Queries.GetCategories;

public record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private const string CacheKey = "categories:all";
    private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(60);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<GetCategoriesQueryHandler> _logger;

    public GetCategoriesQueryHandler(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ILogger<GetCategoriesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        // 1. Kiểm tra IMemoryCache với key "categories:all" (Happy Path 3)
        if (_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<CategoryDto>? cached) && cached is not null)
        {
            _logger.LogInformation("Cache HIT for '{CacheKey}'", CacheKey);
            return cached;
        }

        // 2. Cache miss: query database via UnitOfWork
        var categoriesWithCount = await _unitOfWork.Categories.GetAllWithRecipeCountAsync(ct);

        // 3. Map sang CategoryDto[] và sắp xếp theo Name tăng dần
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

        // 4. Lưu vào IMemoryCache với TTL 60 phút (sliding expiration)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheSlidingExpiration,
            Priority = CacheItemPriority.High
        };

        _memoryCache.Set(CacheKey, (IReadOnlyList<CategoryDto>)categoryDtos, cacheOptions);
        return categoryDtos;
    }
}`
  },

  getCategoryBySlug: {
    title: 'GetCategoryBySlugQueryHandler.cs',
    layer: 'Application (CQRS / MediatR)',
    path: 'src/CulinaryBlog.Application/Categories/Queries/GetCategoryBySlug/GetCategoryBySlugQuery.cs',
    req: 'FR-CAT-002 (OFFSET Pagination & Roles)',
    code: `using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;

public record GetCategoryBySlugQuery(string Slug, int Page = 1, int PageSize = 12) : IRequest<CategoryDetailDto>;

public class GetCategoryBySlugQueryHandler : IRequestHandler<GetCategoryBySlugQuery, CategoryDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetCategoryBySlugQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<CategoryDetailDto> Handle(GetCategoryBySlugQuery request, CancellationToken ct)
    {
        var slug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

        // 1. Tìm category theo slug; ném NotFoundException nếu không tồn tại (RFC 7807)
        var category = await _unitOfWork.Categories.GetBySlugAsync(slug, ct);
        if (category is null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với slug '{slug}'.");
        }

        // 2. Query recipes: Guest (Published), Author (+ Draft của mình), Admin (Tất cả)
        // Áp dụng phân trang OFFSET: SKIP (page-1)*pageSize TAKE pageSize
        var (recipes, totalCount) = await _unitOfWork.Categories.GetRecipesByCategoryAsync(
            category.Id,
            _currentUserService.UserId,
            _currentUserService.IsAdmin,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 50),
            ct
        );

        var recipeDtos = recipes.Select(r => new RecipeSummaryDto(
            r.Id, r.Title, r.Slug, r.Description, r.AuthorId, r.AuthorName,
            r.AuthorAvatar, r.Status.ToString(), r.Difficulty, r.PrepTimeMinutes,
            r.CookTimeMinutes, r.Servings, r.ThumbnailUrl, r.LikeCount, r.CreatedAt
        )).ToList();

        var pagedRecipes = new PagedResult<RecipeSummaryDto>(recipeDtos, totalCount, request.Page, request.PageSize);
        var categoryDto = new CategoryDto(
            category.Id, category.Name, category.Slug, category.Description,
            category.ImageUrl, totalCount, category.OrderIndex
        );

        return new CategoryDetailDto(categoryDto, pagedRecipes);
    }
}`
  },

  createCategory: {
    title: 'CreateCategoryCommandHandler.cs',
    layer: 'Application (CQRS / MediatR)',
    path: 'src/CulinaryBlog.Application/Categories/Commands/CreateCategory/CreateCategoryCommand.cs',
    req: 'FR-CAT-003 (Admin, Slugify & Cache Eviction)',
    code: `using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace CulinaryBlog.Application.Categories.Commands.CreateCategory;

public record CreateCategoryCommand(
    string Name, 
    string? Description = null, 
    string? ImageUrl = null, 
    int OrderIndex = 0
) : IRequest<CategoryDto>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        // Name: 2-50 ký tự, không chứa thẻ HTML (FR-CAT-003)
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .Length(2, 50).WithMessage("Tên danh mục phải từ 2 đến 50 ký tự.")
            .Matches(@"^[^<>]+$").WithMessage("Tên danh mục không được chứa thẻ HTML.");

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(500)
                .Matches(@"^[^<>]+$").WithMessage("Mô tả không được chứa thẻ HTML.");
        });
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private const string CacheKey = "categories:all";
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentUserService _currentUserService;

    public CreateCategoryCommandHandler(
        IUnitOfWork unitOfWork,
        IMemoryCache memoryCache,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _memoryCache = memoryCache;
        _currentUserService = currentUserService;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (!_currentUserService.IsAdmin)
            throw new ForbiddenException("Chỉ Quản trị viên (Admin) mới có quyền tạo danh mục.");

        var trimmedName = request.Name.Trim();

        // Kiểm tra trùng tên -> 409 Conflict
        if (await _unitOfWork.Categories.ExistsByNameAsync(trimmedName, ct))
            throw new ConflictException($"Tên danh mục '{trimmedName}' đã tồn tại.");

        // Sinh slug chuẩn tiếng Việt và thêm hậu tố -2, -3 nếu trùng slug
        var baseSlug = SlugHelper.Generate(trimmedName);
        var candidateSlug = baseSlug;
        var suffix = 2;
        while (await _unitOfWork.Categories.ExistsBySlugAsync(candidateSlug, ct))
        {
            candidateSlug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        // Tạo Entity qua Factory Method
        var category = Category.Create(trimmedName, candidateSlug, request.Description, request.ImageUrl, request.OrderIndex);
        await _unitOfWork.Categories.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // INVALIDATE CACHE: MemoryCache.Remove("categories:all")
        _memoryCache.Remove(CacheKey);

        return new CategoryDto(category.Id, category.Name, category.Slug, category.Description, category.ImageUrl, 0, category.OrderIndex);
    }
}`
  },

  slugHelper: {
    title: 'SlugHelper.cs',
    layer: 'Application (Common / Helpers)',
    path: 'src/CulinaryBlog.Application/Common/Helpers/SlugHelper.cs',
    req: 'FR-CAT-003 (Slugify tiếng Việt)',
    code: `using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Common.Helpers;

public static partial class SlugHelper
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashesRegex();

    public static string Generate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Chữ thường
        var normalized = text.Trim().ToLowerInvariant().Replace("đ", "d").Replace("Đ", "d");

        // 2. Bỏ dấu tiếng Việt qua Unicode Normalization Form D
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var clean = sb.ToString().Normalize(NormalizationForm.FormC);

        // 3. Thay khoảng trắng bằng "-" và bỏ ký tự đặc biệt
        clean = InvalidCharsRegex().Replace(clean, "");
        clean = SpaceRegex().Replace(clean, "-");
        clean = MultipleDashesRegex().Replace(clean, "-");

        return clean.Trim('-');
    }
}`
  },

  program: {
    title: 'Program.cs',
    layer: 'Host & Startup (.NET 10)',
    path: 'src/CulinaryBlog.WebApi/Program.cs',
    req: 'Cấu hình DI, MemoryCache & Pipeline',
    code: `var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình IMemoryCache (SRS FR-CAT-001)
builder.Services.AddMemoryCache();

// 2. Cấu hình MediatR & FluentValidation Pipeline
builder.Services.AddApplicationServices();

// 3. Cấu hình EF Core DbContext & Repositories
builder.Services.AddInfrastructureServices(builder.Configuration);

// 4. Cấu hình Authentication & Authorization
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorization(options => {
    options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
});

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 5. Middleware RFC 7807 Problem Details
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();`
  }
};

export default function DotnetArchitectureModal({ isOpen, onClose }: DotnetArchitectureModalProps) {
  const [activeTab, setActiveTab] = useState<string>('controller');
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const currentSnippet = codeSnippets[activeTab];

  const handleCopy = () => {
    navigator.clipboard.writeText(currentSnippet.code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/75 backdrop-blur-sm animate-in fade-in">
      <div className="w-full max-w-5xl h-[88vh] bg-slate-900 text-slate-100 rounded-3xl shadow-2xl border border-slate-800 flex flex-col overflow-hidden animate-in zoom-in-95">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/80">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-purple-600/30 border border-purple-500/40 text-purple-300 flex items-center justify-center font-mono text-xs font-bold shadow-xs">
              .NET 10
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-white tracking-tight">
                  Kiến Trúc ASP.NET Core (.NET 10) - Module Category (FR-CAT)
                </h2>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-blue-500/20 text-blue-300 border border-blue-500/30">
                  Clean Architecture + MediatR CQRS
                </span>
              </div>
              <p className="text-xs text-slate-400">
                Mã nguồn C# .NET 10 hoàn chỉnh cho FR-CAT-001, FR-CAT-002, FR-CAT-003
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-slate-800 text-slate-400 hover:text-white transition cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Navigation Tabs */}
        <div className="flex items-center gap-1.5 px-6 py-2.5 bg-slate-950 border-b border-slate-800/80 overflow-x-auto text-xs">
          {Object.entries(codeSnippets).map(([key, item]) => {
            const isActive = activeTab === key;
            return (
              <button
                key={key}
                onClick={() => setActiveTab(key)}
                className={`flex items-center gap-2 px-3 py-1.5 rounded-xl font-mono text-[11px] transition whitespace-nowrap cursor-pointer ${
                  isActive
                    ? 'bg-blue-600 text-white font-bold shadow-xs'
                    : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
                }`}
              >
                <FileCode2 className="w-3.5 h-3.5" />
                <span>{item.title}</span>
              </button>
            );
          })}
        </div>

        {/* Code Header Bar */}
        <div className="px-6 py-2 bg-slate-950/40 border-b border-slate-800 flex flex-wrap items-center justify-between text-xs text-slate-400 gap-2">
          <div className="flex items-center gap-2 font-mono text-[11px]">
            <span className="text-blue-400 font-semibold">{currentSnippet.layer}</span>
            <span>•</span>
            <span className="text-slate-500">{currentSnippet.path}</span>
            <span>•</span>
            <span className="px-2 py-0.5 rounded bg-slate-800 text-emerald-400 font-sans text-[10px] font-bold">
              {currentSnippet.req}
            </span>
          </div>

          <button
            onClick={handleCopy}
            className="flex items-center gap-1.5 px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700 transition cursor-pointer"
          >
            {copied ? (
              <>
                <Check className="w-3.5 h-3.5 text-emerald-400" />
                <span className="text-emerald-400">Đã sao chép!</span>
              </>
            ) : (
              <>
                <Copy className="w-3.5 h-3.5 text-slate-400" />
                <span>Sao chép C#</span>
              </>
            )}
          </button>
        </div>

        {/* Code Content */}
        <div className="flex-1 overflow-auto p-6 bg-slate-950 font-mono text-xs text-slate-300 leading-relaxed selection:bg-blue-600/40">
          <pre className="overflow-x-auto whitespace-pre">{currentSnippet.code}</pre>
        </div>

        {/* Footer Architecture Note */}
        <div className="px-6 py-3 bg-slate-950 border-t border-slate-800 flex flex-wrap items-center justify-between gap-3 text-xs text-slate-400">
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1.5 text-slate-300 font-semibold">
              <Database className="w-3.5 h-3.5 text-blue-400" />
              <span>IMemoryCache (TTL 60 min sliding)</span>
            </span>
            <span className="flex items-center gap-1.5 text-slate-300 font-semibold">
              <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
              <span>RFC 7807 Problem Details</span>
            </span>
            <span className="flex items-center gap-1.5 text-slate-300 font-semibold">
              <Cpu className="w-3.5 h-3.5 text-purple-400" />
              <span>MediatR v12 Pipeline Behavior</span>
            </span>
          </div>

          <div className="text-[11px] text-slate-500 font-mono">
            Mã nguồn đã được tạo sẵn trong thư mục <code className="text-blue-400">/aspnet-core-dotnet10</code>
          </div>
        </div>
      </div>
    </div>
  );
}
