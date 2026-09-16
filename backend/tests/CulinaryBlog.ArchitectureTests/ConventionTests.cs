using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// Quy ước đặt tên trong CLAUDE.md mục 4.
/// Các test này bắt đầu có tác dụng từ Sprint 1, khi đã có Command/Query đầu tiên.
/// </summary>
public class ConventionTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(typeof(Application.DependencyInjection).Assembly)
        .Build();

    [Fact(Skip = "Bật từ Sprint 1, khi đã có Command/Query đầu tiên", DisplayName = "Quy ước: lớp tên *Command phải nằm trong thư mục Commands")]
    public void Commands_ShouldResideIn_CommandsNamespace() =>
        Classes().That().HaveNameEndingWith("Command")
            .Should().ResideInNamespace("Commands", true)
            .OrShould().BeAbstract()
            .Check(Architecture);

    [Fact(Skip = "Bật từ Sprint 1, khi đã có Command/Query đầu tiên", DisplayName = "Quy ước: lớp tên *Query phải nằm trong thư mục Queries")]
    public void Queries_ShouldResideIn_QueriesNamespace() =>
        Classes().That().HaveNameEndingWith("Query")
            .Should().ResideInNamespace("Queries", true)
            .OrShould().BeAbstract()
            .Check(Architecture);

    [Fact(Skip = "Bật từ Sprint 1, khi đã có Command/Query đầu tiên", DisplayName = "Quy ước: Handler phải là sealed (không kế thừa tiếp)")]
    public void Handlers_ShouldBeSealed() =>
        Classes().That().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .OrShould().BeAbstract()
            .Check(Architecture);
}
