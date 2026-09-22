using CulinaryBlog.Application.Recipes.Commands.Images;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-RCP-008 / D22 / D23 — validate body của PATCH /images/{imageId}.</summary>
public class UpdateRecipeImageCommandValidatorTests
{
    private readonly UpdateRecipeImageCommandValidator _validator = new();

    private static UpdateRecipeImageCommand Command(string? altText = null, bool? isPrimary = null, int? orderIndex = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), altText, isPrimary, orderIndex);

    [Fact(DisplayName = "FR-RCP-008/D23: PATCH không có field nào → lỗi validation")]
    public async Task EmptyPatch_Fails()
    {
        var result = await _validator.ValidateAsync(Command());

        result.IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "FR-RCP-008/D22: mỗi field đơn lẻ đều hợp lệ")]
    [InlineData("món phở", null, null)]
    [InlineData(null, true, null)]
    [InlineData(null, false, null)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, 5)]
    public async Task SingleField_Passes(string? altText, bool? isPrimary, int? orderIndex)
    {
        var result = await _validator.ValidateAsync(Command(altText, isPrimary, orderIndex));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008/D23: orderIndex âm → lỗi validation")]
    public async Task NegativeOrderIndex_Fails()
    {
        var result = await _validator.ValidateAsync(Command(orderIndex: -1));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeImageCommand.OrderIndex));
    }

    [Fact(DisplayName = "FR-RCP-008/D23: altText > 200 ký tự → lỗi validation")]
    public async Task AltTextTooLong_Fails()
    {
        var result = await _validator.ValidateAsync(Command(altText: new string('a', 201)));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeImageCommand.AltText));
    }
}
