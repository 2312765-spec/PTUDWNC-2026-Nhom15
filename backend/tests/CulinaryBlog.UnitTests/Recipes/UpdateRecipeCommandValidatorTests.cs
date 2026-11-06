using CulinaryBlog.Application.Recipes.Commands.Update;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>FR-RCP-004 — D69 (body đầy đủ) + D71 (If-Match bắt buộc, base64).</summary>
public class UpdateRecipeCommandValidatorTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly string RowVersion = Convert.ToBase64String([1, 2, 3, 4]);
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();

    public UpdateRecipeCommandValidatorTests()
    {
        _categories.GetByIdAsync(CategoryId, Arg.Any<CancellationToken>()).Returns(Category.Create("Món cuốn", "mon-cuon"));
    }

    private static UpdateRecipeCommand Command(string? ifMatch) =>
        new(Guid.NewGuid(), ifMatch, "Gỏi cuốn chay", "Không tôm thịt", CategoryId, 20, 0, 2, "Medium", null, null);

    [Theory(DisplayName = "FR-RCP-004/D71: If-Match nhận base64 trần, có ngoặc kép, hoặc weak W/\"...\"")]
    [InlineData("AQIDBA==")]
    [InlineData("\"AQIDBA==\"")]
    [InlineData("W/\"AQIDBA==\"")]
    public async Task IfMatch_AcceptedForms_Pass(string ifMatch)
    {
        var result = await new UpdateRecipeCommandValidator(_categories).ValidateAsync(Command(ifMatch));

        result.IsValid.Should().BeTrue();
        RowVersionHeader.TryParse(ifMatch, out var bytes).Should().BeTrue();
        Convert.ToBase64String(bytes).Should().Be(RowVersion);
    }

    [Theory(DisplayName = "FR-RCP-004/D71: thiếu If-Match, '*', hoặc không phải base64 → lỗi trường RowVersion (400)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("\"khong-phai-base64!\"")]
    public async Task IfMatch_MissingOrInvalid_Fails(string? ifMatch)
    {
        var result = await new UpdateRecipeCommandValidator(_categories).ValidateAsync(Command(ifMatch));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RowVersion");
    }

    [Fact(DisplayName = "FR-RCP-004/D69: dùng chung ràng buộc với FR-RCP-003 (title ngắn, category sai)")]
    public async Task SharedRecipeRules_Apply()
    {
        var command = Command(RowVersion) with { Title = "Gỏi", CategoryId = Guid.NewGuid() };

        var result = await new UpdateRecipeCommandValidator(_categories).ValidateAsync(command);

        result.Errors.Select(e => e.PropertyName).Should().Contain(["Title", "CategoryId"]);
    }
}
