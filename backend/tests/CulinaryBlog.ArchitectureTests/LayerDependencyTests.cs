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
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.DependencyInjection).Assembly,
            typeof(Infrastructure.DependencyInjection).Assembly,
            typeof(Program).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Domain", true).As("Domain");

    private static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Application", true).As("Application");

    private static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInNamespace("CulinaryBlog.Infrastructure", true).As("Infrastructure");

    private static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInNamespace("CulinaryBlog.API", true).As("API");

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc Application")]
    public void Domain_ShouldNotDependOn_Application() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc Infrastructure")]
    public void Domain_ShouldNotDependOn_Infrastructure() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-001: Domain không phụ thuộc API")]
    public void Domain_ShouldNotDependOn_Api() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-001: Application KHÔNG được reference Infrastructure")]
    public void Application_ShouldNotDependOn_Infrastructure() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-001: Application không phụ thuộc API")]
    public void Application_ShouldNotDependOn_Api() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-001: Infrastructure không phụ thuộc API")]
    public void Infrastructure_ShouldNotDependOn_Api() =>
        Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Check(Architecture);

    [Fact(DisplayName = "CONS-006: Domain không dùng EF Core (không có ORM trong Domain)")]
    public void Domain_ShouldNotDependOn_EntityFramework() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore", true))
            .Check(Architecture);
}
