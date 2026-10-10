using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// CONS-001 + NFR-MAINT-004 — Dependency Rule của Clean Architecture.
///
///   Domain          ← không tham chiếu ai
///   Application     → Domain (chỉ vậy). KHÔNG reference Infrastructure
///   Infrastructure  → Application, Domain
///   API             → tất cả
///
/// Test này chạy trong CI. Vi phạm = build đỏ, không merge được.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.DependencyInjection).Assembly,
            typeof(Infrastructure.DependencyInjection).Assembly,
            typeof(Program).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _domainLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Domain", true).As("Domain");

    private static readonly IObjectProvider<IType> _applicationLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Application", true).As("Application");

    private static readonly IObjectProvider<IType> _infrastructureLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Infrastructure", true).As("Infrastructure");

    private static readonly IObjectProvider<IType> _apiLayer =
        Types().That().ResideInNamespace("CulinaryBlog.API", true).As("API");

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc Application")]
    public void Domain_ShouldNotDependOn_Application() =>
        Types().That().Are(_domainLayer)
            .Should().NotDependOnAny(_applicationLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc Infrastructure")]
    public void Domain_ShouldNotDependOn_Infrastructure() =>
        Types().That().Are(_domainLayer)
            .Should().NotDependOnAny(_infrastructureLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc API")]
    public void Domain_ShouldNotDependOn_Api() =>
        Types().That().Are(_domainLayer)
            .Should().NotDependOnAny(_apiLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-001: Application KHÔNG được reference Infrastructure")]
    public void Application_ShouldNotDependOn_Infrastructure() =>
        Types().That().Are(_applicationLayer)
            .Should().NotDependOnAny(_infrastructureLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-001: Application không phụ thuộc API")]
    public void Application_ShouldNotDependOn_Api() =>
        Types().That().Are(_applicationLayer)
            .Should().NotDependOnAny(_apiLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-001: Infrastructure không phụ thuộc API")]
    public void Infrastructure_ShouldNotDependOn_Api() =>
        Types().That().Are(_infrastructureLayer)
            .Should().NotDependOnAny(_apiLayer)
            .Check(_architecture);

    [Fact(DisplayName = "CONS-006: Domain không dùng EF Core (không có ORM trong Domain)")]
    public void Domain_ShouldNotDependOn_EntityFramework() =>
        Types().That().Are(_domainLayer)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore", true))

            .Check(_architecture);

        

    [Fact(DisplayName = "CONS-001/D39: Domain và Application không dùng OpenTelemetry (chỉ System.Diagnostics)")]
    public void DomainAndApplication_ShouldNotDependOn_OpenTelemetry() =>
        Types().That().Are(_domainLayer).Or().Are(_applicationLayer)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("OpenTelemetry", true))
            .Check(_architecture);
}
