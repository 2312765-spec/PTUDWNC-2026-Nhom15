using CulinaryBlog.Application.Categories.Commands.UpdateCategory;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>FR-CAT-004 — CONS-008, D4 (validation → 400). Cùng luật với FR-CAT-003 (Name 2-50 ký tự, không HTML).</summary>
public class UpdateCategoryCommandValidatorTests
{
    private readonly UpdateCategoryCommandValidator _validator = new();

    [Fact(DisplayName = "FR-CAT-004: dữ liệu hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), "Món chay", "Không dùng thịt cá"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact(DisplayName = "FR-CAT-004/D4: Name null (body thiếu name) → lỗi validation, không ném NullReferenceException")]
    public void NullName_HasErrorAndDoesNotThrow()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), null!, null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Theory(DisplayName = "FR-CAT-004: Name rỗng, toàn khoảng trắng hoặc dưới 2 ký tự → lỗi")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void InvalidName_HasError(string name)
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), name, null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-004: Name dài 51 ký tự → lỗi")]
    public void NameTooLong_HasError()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), new string('a', 51), null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-004: Id rỗng → lỗi")]
    public void EmptyId_HasError()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.Empty, "Món chay", null));

        result.ShouldHaveValidationErrorFor(c => c.Id);
    }

    [Fact(DisplayName = "FR-CAT-004: Name chứa thẻ HTML → lỗi")]
    public void NameWithHtml_HasError()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), "Món <b>chay</b>", null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-004: Name có ký tự > đứng riêng (không phải thẻ HTML) vẫn hợp lệ, như FR-CAT-003")]
    public void NameWithLoneAngleBracket_IsValid()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), "Cơm > Phở", null));

        result.ShouldNotHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-004: Description chứa <script> → lỗi (không cho lách luật HTML của FR-CAT-003)")]
    public void DescriptionWithHtml_HasError()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), "Món chay", "<script>alert(1)</script>"));

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact(DisplayName = "FR-CAT-004: Description dài 501 ký tự → lỗi")]
    public void DescriptionTooLong_HasError()
    {
        var result = _validator.TestValidate(new UpdateCategoryCommand(Guid.NewGuid(), "Món chay", new string('a', 501)));

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }
}
