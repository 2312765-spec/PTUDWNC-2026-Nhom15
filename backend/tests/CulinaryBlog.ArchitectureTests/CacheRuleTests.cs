using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// NFR-SCALE-001 / D8 — API stateless, cache CHỈ Redis: không assembly nào của hệ thống dùng
/// <c>IMemoryCache</c>, Distributed Memory Cache, HybridCache hay ASP.NET Output Cache (cache trong bộ nhớ
/// process làm các instance trả dữ liệu lệch nhau khi scale ngang).
///
/// Quét IL bằng Mono.Cecil như <see cref="SecurityRuleTests"/> (thấy cả lời gọi trong state machine async).
/// </summary>
public class CacheRuleTests
{
    private static readonly string[] ForbiddenNamespaces =
    [
        "Microsoft.Extensions.Caching.Memory",
        "Microsoft.Extensions.Caching.Hybrid",
        "Microsoft.AspNetCore.OutputCaching",
    ];

    private static readonly string[] ForbiddenMethods =
        ["AddMemoryCache", "AddDistributedMemoryCache", "AddHybridCache", "AddOutputCache", "UseOutputCache", "CacheOutput"];

    private static readonly IReadOnlyList<(string Assembly, string TypeRef, IReadOnlyList<string> Calls)> Modules = Load(
        typeof(Domain.Common.BaseEntity).Assembly.Location,
        typeof(Application.DependencyInjection).Assembly.Location,
        typeof(Infrastructure.DependencyInjection).Assembly.Location,
        typeof(Program).Assembly.Location);

    [Fact(DisplayName = "NFR-SCALE-001/D8: không assembly nào tham chiếu IMemoryCache / HybridCache / Output Cache")]
    public void NoAssembly_ReferencesInProcessCache()
    {
        Modules.Where(m => ForbiddenNamespaces.Any(ns => m.TypeRef.StartsWith(ns + ".", StringComparison.Ordinal)))
            .Select(m => $"{m.Assembly}: {m.TypeRef}")
            .Should().BeEmpty("D8: chỉ Redis — cache trong process không chia sẻ được giữa các instance");
    }

    [Fact(DisplayName = "NFR-SCALE-001/D8: không gọi AddMemoryCache / AddDistributedMemoryCache / AddOutputCache…")]
    public void NoAssembly_RegistersInProcessCache()
    {
        Modules.SelectMany(m => m.Calls.Select(c => (m.Assembly, Call: c)))
            .Where(x => ForbiddenMethods.Any(f => x.Call.EndsWith("::" + f, StringComparison.Ordinal)))
            .Select(x => $"{x.Assembly}: {x.Call}")
            .Distinct()
            .Should().BeEmpty("D8: chỉ Redis");
    }

    [Fact(DisplayName = "NFR-SCALE-001 (đối chứng): bộ quét thấy tham chiếu Redis đang có trong Infrastructure")]
    public void Scan_DetectsRedisReference()
    {
        // Nếu không thấy StackExchange.Redis thì hai test trên luôn xanh vô nghĩa.
        Modules.Should().Contain(m =>
            m.Assembly == "CulinaryBlog.Infrastructure" && m.TypeRef == "StackExchange.Redis.IConnectionMultiplexer");
    }

    private static List<(string, string, IReadOnlyList<string>)> Load(params string[] assemblyPaths)
    {
        var result = new List<(string, string, IReadOnlyList<string>)>();
        foreach (var path in assemblyPaths)
        {
            using var module = ModuleDefinition.ReadModule(path);
            var name = module.Assembly.Name.Name;
            var calls = (
                from type in module.GetTypes()
                from method in type.Methods.Where(m => m.HasBody)
                from instruction in method.Body.Instructions
                where instruction.OpCode.FlowControl == FlowControl.Call && instruction.Operand is MethodReference
                let callee = (MethodReference)instruction.Operand
                select $"{callee.DeclaringType.FullName}::{callee.Name}").ToList();

            result.Add((name, string.Empty, calls));
            result.AddRange(module.GetTypeReferences().Select(t => (name, t.FullName, (IReadOnlyList<string>)[])));
        }

        return result;
    }
}
