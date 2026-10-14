using FluentValidation;

namespace CulinaryBlog.Application.Seo.Commands.GenerateSitemap;

/// <summary>FR-JOB-003/D52 — thiếu Seo:SiteBaseUrl thì job lỗi (Hangfire retry rồi Failed), không ghi sitemap.</summary>
public sealed class GenerateSitemapCommandValidator : AbstractValidator<GenerateSitemapCommand>
{
    public GenerateSitemapCommandValidator()
    {
        RuleFor(x => x.SiteBaseUrl)
            .NotEmpty()
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("Seo:SiteBaseUrl phải là URL tuyệt đối http(s).");
    }

    private static bool BeAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
