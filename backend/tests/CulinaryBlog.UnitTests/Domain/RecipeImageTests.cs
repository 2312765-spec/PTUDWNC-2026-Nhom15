using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>FR-RCP-008 / D22 / D23 — logic IsPrimary nằm ở Domain, không ở handler.</summary>
public class RecipeImageTests
{
    private static Recipe NewRecipe() =>
        Recipe.Create("Phở bò tái nạm", "pho-bo-tai-nam", "Món phở truyền thống", 30, 120, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), "author-1");

    [Fact(DisplayName = "FR-RCP-008/D22: ảnh đầu tiên tự động là primary, orderIndex = 0")]
    public void FirstImage_IsPrimary()
    {
        var recipe = NewRecipe();

        var image = recipe.AttachImage("http://minio/a.jpg", "alt");

        image.IsPrimary.Should().BeTrue();
        image.OrderIndex.Should().Be(0);
        image.AltText.Should().Be("alt");
        recipe.Images.Should().ContainSingle();
    }

    [Fact(DisplayName = "FR-RCP-008/D22+D23: ảnh thứ hai không primary, orderIndex = Max + 1")]
    public void SecondImage_IsNotPrimary_OrderIsMaxPlusOne()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        recipe.UpdateImage(first.Id, null, null, 7);

        var second = recipe.AttachImage("http://minio/b.jpg");

        second.IsPrimary.Should().BeFalse();
        second.OrderIndex.Should().Be(8);
        first.IsPrimary.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008/D22: PATCH isPrimary=true → ảnh này primary, ảnh khác về false")]
    public void SetPrimary_UnsetsOthers()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        var second = recipe.AttachImage("http://minio/b.jpg");

        recipe.UpdateImage(second.Id, null, true, null);

        second.IsPrimary.Should().BeTrue();
        first.IsPrimary.Should().BeFalse();
        recipe.Images.Count(i => i.IsPrimary).Should().Be(1);
    }

    [Fact(DisplayName = "FR-RCP-008/D22: PATCH isPrimary=false trên ảnh primary → RECIPE_PRIMARY_IMAGE_REQUIRED")]
    public void UnsetPrimary_OnPrimaryImage_Throws()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        recipe.AttachImage("http://minio/b.jpg");

        var act = () => recipe.UpdateImage(first.Id, null, false, null);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_PRIMARY_IMAGE_REQUIRED");
        first.IsPrimary.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008/D22: PATCH isPrimary=false trên ảnh không primary → không đổi gì")]
    public void UnsetPrimary_OnNonPrimaryImage_IsNoOp()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        var second = recipe.AttachImage("http://minio/b.jpg");

        recipe.UpdateImage(second.Id, null, false, null);

        first.IsPrimary.Should().BeTrue();
        second.IsPrimary.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-RCP-008/D23: PATCH cập nhật altText và orderIndex, orderIndex được phép trùng")]
    public void Update_AltTextAndOrderIndex()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        var second = recipe.AttachImage("http://minio/b.jpg");

        recipe.UpdateImage(second.Id, "món mới", null, first.OrderIndex);

        second.AltText.Should().Be("món mới");
        second.OrderIndex.Should().Be(first.OrderIndex);
    }

    [Fact(DisplayName = "FR-RCP-008/D22: xóa ảnh primary → ảnh còn lại có orderIndex nhỏ nhất lên primary")]
    public void RemovePrimary_PromotesLowestOrderIndex()
    {
        var recipe = NewRecipe();
        var primary = recipe.AttachImage("http://minio/a.jpg");
        var b = recipe.AttachImage("http://minio/b.jpg");
        var c = recipe.AttachImage("http://minio/c.jpg");
        recipe.UpdateImage(b.Id, null, null, 9);
        recipe.UpdateImage(c.Id, null, null, 2);

        var removed = recipe.RemoveImage(primary.Id);

        removed.Id.Should().Be(primary.Id);
        recipe.Images.Should().HaveCount(2);
        c.IsPrimary.Should().BeTrue();
        b.IsPrimary.Should().BeFalse();
    }

    [Fact(DisplayName = "FR-RCP-008: xóa ảnh không primary → primary giữ nguyên")]
    public void RemoveNonPrimary_KeepsPrimary()
    {
        var recipe = NewRecipe();
        var first = recipe.AttachImage("http://minio/a.jpg");
        var second = recipe.AttachImage("http://minio/b.jpg");

        recipe.RemoveImage(second.Id);

        first.IsPrimary.Should().BeTrue();
        recipe.Images.Should().ContainSingle();
    }

    [Fact(DisplayName = "FR-RCP-008: xóa ảnh duy nhất → recipe không còn ảnh, không lỗi")]
    public void RemoveOnlyImage_LeavesNoImages()
    {
        var recipe = NewRecipe();
        var only = recipe.AttachImage("http://minio/a.jpg");

        recipe.RemoveImage(only.Id);

        recipe.Images.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-008/D22: sau khi xóa hết rồi upload lại, ảnh mới lại là primary")]
    public void AttachAfterRemovingAll_IsPrimaryAgain()
    {
        var recipe = NewRecipe();
        var only = recipe.AttachImage("http://minio/a.jpg");
        recipe.RemoveImage(only.Id);

        var again = recipe.AttachImage("http://minio/b.jpg");

        again.IsPrimary.Should().BeTrue();
    }
}
