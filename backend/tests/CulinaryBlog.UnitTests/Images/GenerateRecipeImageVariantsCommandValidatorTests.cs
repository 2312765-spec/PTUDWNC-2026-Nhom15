using CulinaryBlog.Application.Recipes.Commands.Images;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-JOB-002 — CONS-008: command nội bộ vẫn validate qua pipeline.</summary>
public class GenerateRecipeImageVariantsCommandValidatorTests
{
    private readonly GenerateRecipeImageVariantsCommandValidator _validator = new();

    [Fact(DisplayName = "FR-JOB-002: RecipeId và ImageId hợp lệ → pass")]
    public void Valid_Passes() =>
        _validator.Validate(new GenerateRecipeImageVariantsCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact(DisplayName = "FR-JOB-002: RecipeId rỗng → fail")]
    public void EmptyRecipeId_Fails() =>
        _validator.Validate(new GenerateRecipeImageVariantsCommand(Guid.Empty, Guid.NewGuid())).IsValid.Should().BeFalse();

    [Fact(DisplayName = "FR-JOB-002: ImageId rỗng → fail")]
    public void EmptyImageId_Fails() =>
        _validator.Validate(new GenerateRecipeImageVariantsCommand(Guid.NewGuid(), Guid.Empty)).IsValid.Should().BeFalse();
}
