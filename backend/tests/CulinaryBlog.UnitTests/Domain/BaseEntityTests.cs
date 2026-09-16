using CulinaryBlog.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>
/// D1/D2 — soft delete. Test mẫu để cả nhóm thấy khung test chạy được từ ngày đầu.
/// </summary>
public class BaseEntityTests
{
    private sealed class TestEntity : BaseEntity;

    [Fact(DisplayName = "D1: entity mới tạo có IsDeleted = false")]
    public void NewEntity_IsNotDeleted()
    {
        var entity = new TestEntity();

        entity.IsDeleted.Should().BeFalse();
        entity.Id.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "D1: SoftDelete() đặt IsDeleted = true, không xóa vật lý")]
    public void SoftDelete_SetsFlag()
    {
        var entity = new TestEntity();
        var originalId = entity.Id;

        entity.SoftDelete();

        entity.IsDeleted.Should().BeTrue();
        entity.Id.Should().Be(originalId, "soft delete không được làm mất bản ghi");
    }

    [Fact(DisplayName = "D1: Restore() khôi phục được entity đã soft delete")]
    public void Restore_ClearsFlag()
    {
        var entity = new TestEntity();
        entity.SoftDelete();

        entity.Restore();

        entity.IsDeleted.Should().BeFalse();
    }
}
