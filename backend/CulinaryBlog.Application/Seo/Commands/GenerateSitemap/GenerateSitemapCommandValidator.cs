using FluentValidation;

namespace CulinaryBlog.Application.Seo.Commands.GenerateSitemap;

/// <summary>FR-JOB-003/D52 — thiếu Seo:SiteBaseUrl thì job lỗi (Hangfire retry rồi Failed), không ghi sitemap.</summary>
public sealed class GenerateSitemapCommandValidator : AbstractValidator<GenerateSitemapCommand>
{
}
