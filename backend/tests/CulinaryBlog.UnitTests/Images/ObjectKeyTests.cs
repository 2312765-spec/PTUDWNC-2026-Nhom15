using CulinaryBlog.Application.Common.Files;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-FILE-001 / NFR-SEC-004 — tên object sinh bằng GUID, chống path traversal.</summary>
public class ObjectKeyTests
{
    [Fact(DisplayName = "FR-FILE-001: key có dạng {folder}/{Guid}{ext}")]
    public void Create_ProducesFolderGuidExt()
    {
        var recipeId = Guid.NewGuid();

        var key = ObjectKey.Create($"recipes/{recipeId}", ".jpg");

        key.Should().MatchRegex($"^recipes/{recipeId}/[0-9a-f]{{8}}-[0-9a-f]{{4}}-[0-9a-f]{{4}}-[0-9a-f]{{4}}-[0-9a-f]{{12}}\\.jpg$");
    }

    [Fact(DisplayName = "FR-FILE-001: hai lần gọi sinh hai key khác nhau")]
    public void Create_IsUnique() =>
        ObjectKey.Create("recipes/x", ".png").Should().NotBe(ObjectKey.Create("recipes/x", ".png"));

    [Theory(DisplayName = "NFR-SEC-004: đuôi file chứa ký tự path bị từ chối")]
    [InlineData("../../evil.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData(".jpg/../x")]
    [InlineData(".exe")]
    [InlineData("jpg")]
    [InlineData("")]
    public void Create_RejectsUnsafeExtension(string extension)
    {
        var act = () => ObjectKey.Create("recipes/x", extension);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "NFR-SEC-004: folder chứa '..' hoặc tuyệt đối bị từ chối")]
    [InlineData("recipes/../secrets")]
    [InlineData("../recipes")]
    [InlineData("/recipes")]
    [InlineData("recipes\\x")]
    public void Create_RejectsUnsafeFolder(string folder)
    {
        var act = () => ObjectKey.Create(folder, ".jpg");

        act.Should().Throw<ArgumentException>();
    }
}
