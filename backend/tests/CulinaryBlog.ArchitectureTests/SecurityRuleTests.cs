using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// NFR-SEC-004 / CONS-006 — không raw SQL dạng chuỗi. Chỉ cho phép LINQ hoặc API <c>*Interpolated</c>
/// / <c>FromSql</c> (EF Core tự tham số hóa từng giá trị nội suy). Chặn hồi quy: ai thêm
/// <c>FromSqlRaw</c>, <c>ExecuteSqlRaw</c>, <c>SqlQueryRaw</c> thì test đỏ.
///
/// Quét IL bằng Mono.Cecil thay vì ArchUnitNET: ở Release, state machine của phương thức <c>async</c> là
/// <b>struct</b> lồng do compiler sinh và ArchUnitNET không thấy lời gọi bên trong (CI chạy Release —
/// test đối chứng bắt được điều này). Ở đây duyệt mọi kiểu, kể cả kiểu lồng, nên Debug và Release như nhau.
/// </summary>
public class SecurityRuleTests
{
    private static readonly string[] RawSqlMethods = ["FromSqlRaw", "ExecuteSqlRaw", "ExecuteSqlRawAsync", "SqlQueryRaw"];

    private static readonly IReadOnlyList<(string Caller, string Callee)> Calls = LoadCalls(
        typeof(Domain.Common.BaseEntity).Assembly.Location,
        typeof(Application.DependencyInjection).Assembly.Location,
        typeof(Infrastructure.DependencyInjection).Assembly.Location,
        typeof(Program).Assembly.Location);

    [Fact(DisplayName = "NFR-SEC-004/CONS-006: không lớp nào gọi FromSqlRaw / ExecuteSqlRaw / SqlQueryRaw")]
    public void NoType_ShouldCall_RawSqlApis()
    {
        var violations = Calls.Where(c => RawSqlMethods.Any(m => c.Callee.EndsWith("::" + m, StringComparison.Ordinal)));

        violations.Should().BeEmpty(
            "raw SQL dạng chuỗi dễ bị SQL injection — dùng LINQ hoặc API *Interpolated (tham số hóa)");
    }

    [Fact(DisplayName = "NFR-SEC-004 (đối chứng): quét IL thấy được lời gọi EF Core trong phương thức async — bắt được ExecuteSqlInterpolatedAsync đang có")]
    public void Scan_DetectsEfCoreCallInsideAsyncMethod()
    {
        // RecipeRepository.LockForUpdateAsync (async) gọi ExecuteSqlInterpolatedAsync — nếu không thấy thì
        // test trên luôn xanh vô nghĩa.
        Calls.Should().Contain(c =>
            c.Caller.StartsWith("CulinaryBlog.Infrastructure.Persistence.Repositories.RecipeRepository", StringComparison.Ordinal)
            && c.Callee.EndsWith("::ExecuteSqlInterpolatedAsync", StringComparison.Ordinal));
    }

    /// <summary>(kiểu gọi — kể cả kiểu lồng compiler sinh, phương thức được gọi dạng "KiểuKhaiBáo::Tên").</summary>
    private static List<(string Caller, string Callee)> LoadCalls(params string[] assemblyPaths)
    {
        var calls = new List<(string, string)>();
        foreach (var path in assemblyPaths)
        {
            using var module = ModuleDefinition.ReadModule(path);
            foreach (var type in module.GetTypes()) // GetTypes() gồm cả kiểu lồng
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode.FlowControl == FlowControl.Call && instruction.Operand is MethodReference callee)
                        {
                            calls.Add((type.FullName, $"{callee.DeclaringType.FullName}::{callee.Name}"));
                        }
                    }
                }
            }
        }

        return calls;
    }
}
