using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

/// <summary>FR-CAT-003 — CONS-008, decisions.md "Ràng buộc validator" (Category.Name 2-50 ký tự, không HTML).</summary>
public class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    [Fact(DisplayName = "FR-CAT-003: dữ liệu hợp lệ không có lỗi validation")]
    public void Valid_HasNoErrors()
    {
        var result = _validator.TestValidate(new CreateCategoryCommand("Món chay", "Không dùng thịt cá"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory(DisplayName = "FR-CAT-003: Name rỗng hoặc ngoài khoảng 2-50 ký tự → lỗi")]
    [InlineData("")]
    [InlineData("A")]
    public void InvalidName_HasError(string name)
    {
        var result = _validator.TestValidate(new CreateCategoryCommand(name, null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-003: Name dài 51 ký tự → lỗi")]
    public void NameTooLong_HasError()
    {
        var result = _validator.TestValidate(new CreateCategoryCommand(new string('a', 51), null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-003: Name chứa HTML → lỗi")]
    public void NameWithHtml_HasError()
    {
        var result = _validator.TestValidate(new CreateCategoryCommand("<script>alert(1)</script>", null));

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact(DisplayName = "FR-CAT-003: Description chứa HTML → lỗi")]
    public void DescriptionWithHtml_HasError()
    {
        var result = _validator.TestValidate(new CreateCategoryCommand("Món chay", "<b>đậm</b>"));

        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact(DisplayName = "FR-CAT-003: Description null hoặc rỗng vẫn hợp lệ (optional)")]
    public void DescriptionNull_IsValid()
    {
        var result = _validator.TestValidate(new CreateCategoryCommand("Món chay", null));

        result.ShouldNotHaveValidationErrorFor(c => c.Description);
    }
}
