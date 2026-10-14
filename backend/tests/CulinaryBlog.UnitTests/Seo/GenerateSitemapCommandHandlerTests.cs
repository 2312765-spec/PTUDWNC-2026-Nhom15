using System.Xml.Linq;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Seo;
using CulinaryBlog.Application.Seo.Commands.GenerateSitemap;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Seo;

/// <summary>FR-JOB-003 — D52: danh sách URL, lastmod trang tĩnh, key lưu cố định.</summary>
public class GenerateSitemapCommandHandlerTests
{
    private static readonly XNamespace Ns = SitemapXmlBuilder.Namespace;

    private readonly ISitemapRepository _repository = Substitute.For<ISitemapRepository>();
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private byte[] _saved = [];

    public GenerateSitemapCommandHandlerTests()
    {
        _fileStorage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                using var buffer = new MemoryStream();
                call.Arg<Stream>().CopyTo(buffer);
                _saved = buffer.ToArray();
                return "https://minio/culinary-blog/seo/sitemap.xml";
            });
    }

    private GenerateSitemapCommandHandler Handler() =>
        new(_repository, _fileStorage, NullLogger<GenerateSitemapCommandHandler>.Instance);

    [Fact(DisplayName = "FR-JOB-003/D52: 3 trang tĩnh + category + recipe, trả về tổng số URL, lưu seo/sitemap.xml")]
    public async Task Handle_BuildsAllUrls_ReturnsCount()
    {
        Given(
            recipes: [new("pho-bo", Utc(2026, 9, 1)), new("bun-cha", Utc(2026, 9, 5))],
            categories: [new("mon-nuoc", Utc(2026, 8, 1))]);

        var count = await Handler().Handle(new GenerateSitemapCommand("https://site.test"), CancellationToken.None);

        count.Should().Be(6);
        Locs().Should().Equal(
            "https://site.test/",
            "https://site.test/recipes",
            "https://site.test/categories",
            "https://site.test/categories/mon-nuoc",
            "https://site.test/recipes/pho-bo",
            "https://site.test/recipes/bun-cha");
        await _fileStorage.Received(1).SaveAsync(
            GenerateSitemapCommandHandler.ObjectKey, Arg.Any<Stream>(), "application/xml", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "FR-JOB-003/D52: SiteBaseUrl có dấu / cuối → không sinh // trong loc")]
    public async Task Handle_TrailingSlash_IsTrimmed()
    {
        Given(recipes: [new("pho-bo", Utc(2026, 9, 1))], categories: []);

        await Handler().Handle(new GenerateSitemapCommand("https://site.test/"), CancellationToken.None);

        Locs().Should().Contain(["https://site.test/", "https://site.test/recipes/pho-bo"]);
        Locs().Select(l => l["https://".Length..]).Should().NotContain(l => l.Contains("//"));
    }

    [Fact(DisplayName = "FR-JOB-003: lastmod của /, /recipes = recipe mới nhất; /categories = category mới nhất")]
    public async Task Handle_StaticPagesLastmod_IsLatestContent()
    {
        Given(
            recipes: [new("a", Utc(2026, 9, 1)), new("b", Utc(2026, 9, 20))],
            categories: [new("c", Utc(2026, 7, 3)), new("d", Utc(2026, 8, 4))]);

        await Handler().Handle(new GenerateSitemapCommand("https://site.test"), CancellationToken.None);

        Lastmod("https://site.test/").Should().Be("2026-09-20T00:00:00Z");
        Lastmod("https://site.test/recipes").Should().Be("2026-09-20T00:00:00Z");
        Lastmod("https://site.test/categories").Should().Be("2026-08-04T00:00:00Z");
    }

    [Fact(DisplayName = "FR-JOB-003: chưa có recipe/category nào → vẫn có 3 trang tĩnh, không có lastmod")]
    public async Task Handle_NoContent_StaticPagesWithoutLastmod()
    {
        Given(recipes: [], categories: []);

        var count = await Handler().Handle(new GenerateSitemapCommand("https://site.test"), CancellationToken.None);

        count.Should().Be(3);
        Urls().Should().AllSatisfy(u => u.Element(Ns + "lastmod").Should().BeNull());
    }

    [Fact(DisplayName = "FR-JOB-003/D52: priority + changefreq theo loại trang")]
    public async Task Handle_PriorityAndChangefreq_ByPageType()
    {
        Given(recipes: [new("pho-bo", Utc(2026, 9, 1))], categories: [new("mon-nuoc", Utc(2026, 8, 1))]);

        await Handler().Handle(new GenerateSitemapCommand("https://site.test"), CancellationToken.None);

        Meta("https://site.test/").Should().Be(("daily", "1.0"));
        Meta("https://site.test/recipes").Should().Be(("daily", "0.9"));
        Meta("https://site.test/recipes/pho-bo").Should().Be(("weekly", "0.8"));
        Meta("https://site.test/categories").Should().Be(("weekly", "0.7"));
        Meta("https://site.test/categories/mon-nuoc").Should().Be(("weekly", "0.7"));
    }

    private void Given(IReadOnlyList<SitemapEntry> recipes, IReadOnlyList<SitemapEntry> categories)
    {
        _repository.GetPublishedRecipeEntriesAsync(Arg.Any<CancellationToken>()).Returns(recipes);
        _repository.GetCategoryEntriesAsync(Arg.Any<CancellationToken>()).Returns(categories);
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private List<XElement> Urls() =>
        XDocument.Load(new MemoryStream(_saved)).Root!.Elements(Ns + "url").ToList();

    private List<string> Locs() => Urls().Select(u => u.Element(Ns + "loc")!.Value).ToList();

    private XElement Url(string loc) => Urls().Single(u => u.Element(Ns + "loc")!.Value == loc);

    private string? Lastmod(string loc) => Url(loc).Element(Ns + "lastmod")?.Value;

    private (string, string) Meta(string loc) =>
        (Url(loc).Element(Ns + "changefreq")!.Value, Url(loc).Element(Ns + "priority")!.Value);
}
