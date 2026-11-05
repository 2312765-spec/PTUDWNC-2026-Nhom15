using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FluentAssertions;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// NFR-SEC-004 / CONS-006 — không raw SQL dạng chuỗi. Chỉ cho phép LINQ hoặc API <c>*Interpolated</c>
/// / <c>FromSql</c> (EF Core tự tham số hóa từng giá trị nội suy). Chặn hồi quy: ai thêm
/// <c>FromSqlRaw</c>, <c>ExecuteSqlRaw</c>, <c>SqlQueryRaw</c> thì build test đỏ.
/// </summary>
public class SecurityRuleTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.DependencyInjection).Assembly,
            typeof(Infrastructure.DependencyInjection).Assembly,
            typeof(Program).Assembly)
        .Build();

    // Khớp FullName của phương thức EF Core (Mono.Cecil ghi "Kiểu::Tên(...)", không phải "Kiểu.Tên(").
    private const string RawSqlPattern = @"::(FromSqlRaw|ExecuteSqlRaw|ExecuteSqlRawAsync|SqlQueryRaw)(<.*>)?\(";

    [Fact(DisplayName = "NFR-SEC-004/CONS-006: không lớp nào gọi FromSqlRaw / ExecuteSqlRaw / SqlQueryRaw")]
    public void NoType_ShouldCall_RawSqlApis() =>
        Types().Should().NotCallAny(RawSqlPattern, useRegularExpressions: true)
            .Because("raw SQL dạng chuỗi dễ bị SQL injection — dùng LINQ hoặc API *Interpolated (tham số hóa)")
            .Check(Architecture);

    [Fact(DisplayName = "NFR-SEC-004 (đối chứng): quy tắc thấy được lời gọi EF Core — bắt được ExecuteSqlInterpolatedAsync đang có")]
    public void Rule_DetectsExternalEfCoreCalls()
    {
        // Nếu ArchUnitNET không nhìn thấy lời gọi sang EF Core thì test trên luôn xanh vô nghĩa.
        var rule = Types().Should().NotCallAny(@"::ExecuteSqlInterpolatedAsync(<.*>)?\(", useRegularExpressions: true);

        rule.HasNoViolations(Architecture).Should().BeFalse(
            "RecipeRepository.LockForUpdateAsync gọi ExecuteSqlInterpolatedAsync — quy tắc phải phát hiện được");
    }
}
