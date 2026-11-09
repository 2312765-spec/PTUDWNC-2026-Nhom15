using CulinaryBlog.Application.Common.Interfaces;
using FluentAssertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace CulinaryBlog.ArchitectureTests;

/// <summary>
/// NFR-MAINT-004 / CONS-002 — CQRS không trộn read/write: phía đọc (<c>*.Queries.*</c>) không ghi DB, không xóa
/// cache, không đụng file, không gửi Command. Phía ghi không cache kết quả.
///
/// Quét IL bằng Mono.Cecil (như <see cref="SecurityRuleTests"/>): ở Release, thân phương thức <c>async</c> nằm trong
/// struct lồng do compiler sinh mà ArchUnitNET không thấy. Kiểu lồng có <c>Namespace</c> rỗng trong Cecil nên mọi
/// điều kiện "thuộc Queries" đều xét theo <c>FullName</c> (gồm tên kiểu chứa nó).
/// </summary>
public class CqrsSeparationTests
{
    /// <summary>Method ghi của repository / unit of work (Domain.Interfaces).</summary>
    private static readonly string[] WriteMethods =
        ["AddAsync", "TryAddAsync", "Update", "Delete", "SaveStepsAsync", "SaveChangesAsync", "ExecuteInTransactionAsync"];

    /// <summary>Phụ thuộc chỉ phía ghi được dùng.</summary>
    private static readonly string[] WriteSideTypes =
    [
        "CulinaryBlog.Domain.Interfaces.IUnitOfWork",
        typeof(ICacheInvalidator).FullName!,
        typeof(IFileStorageService).FullName!,
    ];

    private static readonly ModuleDefinition Application =
        ModuleDefinition.ReadModule(typeof(Application.DependencyInjection).Assembly.Location);

    private static readonly IReadOnlyList<TypeDefinition> AllTypes = Application.GetTypes().ToList();

    private static bool IsQuerySide(TypeDefinition type) => type.FullName.Contains(".Queries.", StringComparison.Ordinal);

    private static bool IsCommandSide(TypeDefinition type) => type.FullName.Contains(".Commands.", StringComparison.Ordinal);

    private static IEnumerable<(TypeDefinition Type, Instruction Instruction)> Instructions(Func<TypeDefinition, bool> filter) =>
        from type in AllTypes.Where(filter)
        from method in type.Methods.Where(m => m.HasBody)
        from instruction in method.Body.Instructions
        select (type, instruction);

    private static IEnumerable<(TypeDefinition Caller, MethodReference Callee)> Calls(Func<TypeDefinition, bool> filter) =>
        from item in Instructions(filter)
        where item.Instruction.OpCode.FlowControl == FlowControl.Call && item.Instruction.Operand is MethodReference
        select (item.Type, (MethodReference)item.Instruction.Operand);

    private static string ElementName(TypeReference type) => type.GetElementType().FullName;

    [Fact(DisplayName = "NFR-MAINT-004: Query không implement ICacheInvalidator; Command không implement ICacheable")]
    public void Queries_DoNotInvalidate_Commands_DoNotCache()
    {
        var requests = typeof(Application.DependencyInjection).Assembly.GetTypes().Where(t => !t.IsAbstract).ToList();

        requests.Where(t => t.Name.EndsWith("Query", StringComparison.Ordinal) && typeof(ICacheInvalidator).IsAssignableFrom(t))
            .Select(t => t.FullName).Should().BeEmpty("Query chỉ đọc, không được xóa cache (D8)");
        requests.Where(t => t.Name.EndsWith("Command", StringComparison.Ordinal) && typeof(ICacheable).IsAssignableFrom(t))
            .Select(t => t.FullName).Should().BeEmpty("kết quả Command không được cache (D8)");
    }

    [Fact(DisplayName = "NFR-MAINT-004: phía Query không phụ thuộc IUnitOfWork / ICacheInvalidator / IFileStorageService")]
    public void QuerySide_ShouldNotDependOn_WriteSideServices()
    {
        var fieldViolations =
            from type in AllTypes.Where(IsQuerySide)
            from field in type.Fields
            where WriteSideTypes.Contains(ElementName(field.FieldType))
            select $"{type.FullName}.{field.Name}: {field.FieldType.FullName}";

        var parameterViolations =
            from type in AllTypes.Where(IsQuerySide)
            from method in type.Methods
            from parameter in method.Parameters
            where WriteSideTypes.Contains(ElementName(parameter.ParameterType))
            select $"{type.FullName}::{method.Name}({parameter.ParameterType.FullName})";

        var callViolations =
            from call in Calls(IsQuerySide)
            where WriteSideTypes.Contains(ElementName(call.Callee.DeclaringType))
            select $"{call.Caller.FullName} → {call.Callee.DeclaringType.FullName}::{call.Callee.Name}";

        fieldViolations.Concat(parameterViolations).Concat(callViolations)
            .Should().BeEmpty("phía đọc không ghi DB, không xóa cache, không đụng file (CQRS)");
    }

    [Fact(DisplayName = "NFR-MAINT-004: phía Query không gọi method ghi / khóa dòng của repository")]
    public void QuerySide_ShouldNotCall_RepositoryWriteMethods()
    {
        var violations =
            from call in Calls(IsQuerySide)
            where call.Callee.DeclaringType.Namespace == "CulinaryBlog.Domain.Interfaces"
                && (WriteMethods.Contains(call.Callee.Name)
                    || call.Callee.Name.EndsWith("ForUpdateAsync", StringComparison.Ordinal))
            select $"{call.Caller.FullName} → {call.Callee.DeclaringType.FullName}::{call.Callee.Name}";

        violations.Should().BeEmpty("Query chỉ đọc — ghi hoặc SELECT … FOR UPDATE thuộc về Command");
    }

    [Fact(DisplayName = "NFR-MAINT-004: phía Query không tạo / gửi Command")]
    public void QuerySide_ShouldNotCreate_Commands()
    {
        var violations =
            from item in Instructions(IsQuerySide)
            where item.Instruction.OpCode == OpCodes.Newobj && item.Instruction.Operand is MethodReference
            let created = ((MethodReference)item.Instruction.Operand).DeclaringType
            where created.FullName.Contains(".Commands.", StringComparison.Ordinal)
            select $"{item.Type.FullName} → new {created.FullName}";

        violations.Should().BeEmpty("Query gửi Command là trộn đọc với ghi");
    }

    [Fact(DisplayName = "NFR-MAINT-004 (đối chứng): bộ quét thấy Command gọi SaveChangesAsync và Query tạo GetRecipeDetailQuery")]
    public void Scan_SeesKnownCallsOnBothSides()
    {
        // Không thấy hai lời gọi có thật này thì các test trên xanh vô nghĩa (vd. lọc nhầm kiểu lồng async).
        Calls(IsCommandSide).Should().Contain(c =>
            c.Callee.DeclaringType.FullName == "CulinaryBlog.Domain.Interfaces.IUnitOfWork" && c.Callee.Name == "SaveChangesAsync");

        Instructions(IsQuerySide).Should().Contain(i =>
            i.Instruction.OpCode == OpCodes.Newobj
            && ((MethodReference)i.Instruction.Operand).DeclaringType.Name == "GetRecipeDetailQuery");
    }
}
