using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Domain;

/// <summary>Quy tắc nghiệp vụ của aggregate Recipe — FR-RCP-003 (tạo) + D6, D18, D19.</summary>
public class RecipeTests
{
    private static Recipe NewRecipe(int prepTime = 15, int cookTime = 30, int servings = 4, string? instructions = null) =>
        Recipe.Create("Gỏi cuốn tôm thịt", "goi-cuon", "Món cuốn", prepTime, cookTime, servings,
            RecipeDifficulty.Easy, Guid.NewGuid(), "author-1", instructions: instructions);

    [Fact(DisplayName = "FR-RCP-003: recipe mới luôn ở trạng thái Draft, chưa có PublishedAt")]
    public void Create_StartsAsDraft()
    {
        var recipe = NewRecipe();

        recipe.Status.Should().Be(RecipeStatus.Draft);
        recipe.PublishedAt.Should().BeNull();
        recipe.Slug.Should().Be("goi-cuon");
        recipe.AuthorId.Should().Be("author-1");
    }

    [Fact(DisplayName = "FR-RCP-003/D18: Instructions là tùy chọn — lưu khi có, null khi không gửi")]
    public void Create_StoresOptionalInstructions()
    {
        NewRecipe(instructions: "Cuốn chặt tay").Instructions.Should().Be("Cuốn chặt tay");
        NewRecipe().Instructions.Should().BeNull();
    }

    [Fact(DisplayName = "FR-RCP-003/D19: cookTime = 0 hợp lệ (món không cần nấu)")]
    public void Create_ZeroCookTime_IsAllowed()
    {
        NewRecipe(cookTime: 0).CookTime.Should().Be(0);
    }

    [Theory(DisplayName = "FR-RCP-003/D19: prepTime ≤ 0, cookTime < 0, servings ≤ 0 → DomainException")]
    [InlineData(0, 10, 2)]
    [InlineData(10, -1, 2)]
    [InlineData(10, 10, 0)]
    public void Create_InvalidNumbers_Throws(int prepTime, int cookTime, int servings)
    {
        var act = () => NewRecipe(prepTime, cookTime, servings);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact(DisplayName = "FR-RCP-004/D13: Update đổi mọi trường cơ bản nhưng KHÔNG đổi slug")]
    public void Update_ChangesFields_KeepsSlug()
    {
        var recipe = NewRecipe(instructions: "Cũ");
        var newCategory = Guid.NewGuid();

        recipe.Update("Gỏi cuốn chay", "Không tôm thịt", null, 25, 0, 2, RecipeDifficulty.Medium, newCategory);

        recipe.Title.Should().Be("Gỏi cuốn chay");
        recipe.Slug.Should().Be("goi-cuon", "D13 — slug bất biến sau khi tạo");
        recipe.Description.Should().Be("Không tôm thịt");
        recipe.Instructions.Should().BeNull("D69 — PUT thay toàn bộ, instructions null = xóa");
        recipe.PrepTime.Should().Be(25);
        recipe.CookTime.Should().Be(0);
        recipe.Servings.Should().Be(2);
        recipe.Difficulty.Should().Be(RecipeDifficulty.Medium);
        recipe.CategoryId.Should().Be(newCategory);
        recipe.UpdatedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "FR-RCP-004/D19: Update với servings = 0 → DomainException")]
    public void Update_InvalidNumbers_Throws()
    {
        var act = () => NewRecipe().Update("Gỏi cuốn", "Mô tả", null, 10, 10, 0, RecipeDifficulty.Easy, Guid.NewGuid());

        act.Should().Throw<DomainException>();
    }

    [Fact(DisplayName = "FR-RCP-004: Update không đổi trạng thái (Published vẫn Published)")]
    public void Update_KeepsStatus()
    {
        var recipe = Recipe.Create("Phở bò", "pho-bo", "Phở", 30, 120, 4, RecipeDifficulty.Hard,
            Guid.NewGuid(), "author-1", RecipeStatus.Published);

        recipe.Update("Phở bò tái", "Phở", null, 30, 120, 4, RecipeDifficulty.Hard, recipe.CategoryId);

        recipe.Status.Should().Be(RecipeStatus.Published);
        recipe.PublishedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "FR-RCP-010/D6: AppendStep đánh số Max + 1, bắt đầu từ 1")]
    public void AppendStep_NumbersSequentially()
    {
        var recipe = NewRecipe();

        var first = recipe.AppendStep("Sơ chế", "Luộc tôm", null, null);
        var second = recipe.AppendStep("Cuốn", "Cuốn bánh tráng", 5, "https://cdn/x.jpg");

        first.StepNumber.Should().Be(1);
        second.StepNumber.Should().Be(2);
        second.TimerMinutes.Should().Be(5);
        second.ImageUrl.Should().Be("https://cdn/x.jpg");
        second.RecipeId.Should().Be(recipe.Id);
    }

    private static (Recipe Recipe, RecipeStep[] Steps) RecipeWithSteps(int count, RecipeStatus status = RecipeStatus.Draft)
    {
        var recipe = Recipe.Create("Bánh xèo", "banh-xeo", "Bánh xèo miền Tây", 20, 30, 4,
            RecipeDifficulty.Medium, Guid.NewGuid(), "author-1", status);
        var steps = Enumerable.Range(1, count)
            .Select(i => recipe.AppendStep($"Bước {i}", $"Mô tả {i}", null, null))
            .ToArray();
        return (recipe, steps);
    }

    private static string[] TitlesInOrder(Recipe recipe) =>
        recipe.Steps.OrderBy(s => s.StepNumber).Select(s => s.Title).ToArray();

    private static int[] NumbersInOrder(Recipe recipe) =>
        recipe.Steps.OrderBy(s => s.StepNumber).Select(s => s.StepNumber).ToArray();

    [Theory(DisplayName = "FR-RCP-010/D6: xóa bước đầu / giữa / cuối → renumber liên tục 1, 2, 3")]
    [InlineData(0, new[] { "Bước 2", "Bước 3", "Bước 4" })]
    [InlineData(1, new[] { "Bước 1", "Bước 3", "Bước 4" })]
    [InlineData(3, new[] { "Bước 1", "Bước 2", "Bước 3" })]
    public void RemoveStep_RenumbersContiguously(int index, string[] expectedTitles)
    {
        var (recipe, steps) = RecipeWithSteps(4);

        var removed = recipe.RemoveStep(steps[index].Id);

        removed.Should().BeSameAs(steps[index]);
        recipe.Steps.Should().NotContain(steps[index]);
        TitlesInOrder(recipe).Should().Equal(expectedTitles);
        NumbersInOrder(recipe).Should().Equal(1, 2, 3);
    }

    [Theory(DisplayName = "FR-RCP-010/D6: đổi vị trí bước (chèn vào giữa / lên đầu / xuống cuối) → renumber lại toàn bộ")]
    [InlineData(3, 2, new[] { "Bước 1", "Bước 4", "Bước 2", "Bước 3" })]
    [InlineData(2, 1, new[] { "Bước 3", "Bước 1", "Bước 2", "Bước 4" })]
    [InlineData(0, 4, new[] { "Bước 2", "Bước 3", "Bước 4", "Bước 1" })]
    [InlineData(1, 2, new[] { "Bước 1", "Bước 2", "Bước 3", "Bước 4" })]
    public void UpdateStep_MoveToPosition_Renumbers(int index, int newPosition, string[] expectedTitles)
    {
        var (recipe, steps) = RecipeWithSteps(4);
        var target = steps[index];

        recipe.UpdateStep(target.Id, target.Title, "Mô tả mới", 5, null, newPosition);

        TitlesInOrder(recipe).Should().Equal(expectedTitles);
        NumbersInOrder(recipe).Should().Equal(1, 2, 3, 4);
        target.StepNumber.Should().Be(newPosition);
        target.Description.Should().Be("Mô tả mới");
        target.TimerMinutes.Should().Be(5);
    }

    [Fact(DisplayName = "FR-RCP-010/D6: sửa bước không gửi stepNumber → giữ nguyên vị trí, chỉ đổi nội dung")]
    public void UpdateStep_WithoutPosition_KeepsOrder()
    {
        var (recipe, steps) = RecipeWithSteps(3);

        recipe.UpdateStep(steps[1].Id, "Đổ bột", "Đổ bột vào chảo nóng", null, "https://cdn/b.jpg", null);

        NumbersInOrder(recipe).Should().Equal(1, 2, 3);
        steps[1].StepNumber.Should().Be(2);
        steps[1].Title.Should().Be("Đổ bột");
        steps[1].ImageUrl.Should().Be("https://cdn/b.jpg");
    }

    [Theory(DisplayName = "FR-RCP-010/D73: stepNumber ngoài khoảng 1..số bước → DomainException VALIDATION_ERROR")]
    [InlineData(0)]
    [InlineData(4)]
    public void UpdateStep_PositionOutOfRange_Throws(int position)
    {
        var (recipe, steps) = RecipeWithSteps(3);

        var act = () => recipe.UpdateStep(steps[0].Id, "Bước", "Mô tả", null, null, position);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("VALIDATION_ERROR");
        NumbersInOrder(recipe).Should().Equal(1, 2, 3);
    }

    [Fact(DisplayName = "FR-RCP-010/D3/D73: không xóa được bước cuối cùng của recipe đã Published")]
    public void RemoveStep_LastStepOfPublished_Throws()
    {
        var (recipe, steps) = RecipeWithSteps(1, RecipeStatus.Published);

        var act = () => recipe.RemoveStep(steps[0].Id);

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_PUBLISH_INCOMPLETE");
        recipe.Steps.Should().HaveCount(1);
    }

    [Fact(DisplayName = "FR-RCP-010: xóa bước cuối cùng của Draft → được (Draft được phép chưa có bước)")]
    public void RemoveStep_LastStepOfDraft_Allowed()
    {
        var (recipe, steps) = RecipeWithSteps(1);

        recipe.RemoveStep(steps[0].Id);

        recipe.Steps.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-RCP-010/D73: bước không thuộc recipe → DomainException RECIPE_STEP_NOT_FOUND")]
    public void RemoveStep_UnknownId_Throws()
    {
        var (recipe, _) = RecipeWithSteps(2);

        var act = () => recipe.RemoveStep(Guid.NewGuid());

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("RECIPE_STEP_NOT_FOUND");
    }

    [Fact(DisplayName = "FR-RCP-010/D6: dữ liệu cũ có khoảng trống (1, 3, 7) → thao tác kế tiếp đưa về 1, 2, 3")]
    public void RemoveStep_WithGaps_NormalizesNumbers()
    {
        var recipe = Recipe.Create("Canh chua", "canh-chua", "Canh chua cá lóc", 15, 20, 4,
            RecipeDifficulty.Easy, Guid.NewGuid(), "author-1");
        recipe.AddStep(1, "A", "a");
        recipe.AddStep(3, "B", "b");
        recipe.AddStep(7, "C", "c");
        recipe.AddStep(9, "D", "d");

        recipe.RemoveStep(recipe.Steps.Single(s => s.Title == "D").Id);

        TitlesInOrder(recipe).Should().Equal("A", "B", "C");
        NumbersInOrder(recipe).Should().Equal(1, 2, 3);
    }

    [Fact(DisplayName = "FR-RCP-010/D6: AppendStep sau dữ liệu có khoảng trống vẫn lấy Max + 1")]
    public void AppendStep_AfterGap_UsesMaxPlusOne()
    {
        var recipe = NewRecipe();
        recipe.AddStep(3, "Cũ", "Bước số 3 từ seed");

        recipe.AppendStep("Mới", "Bước mới", null, null).StepNumber.Should().Be(4);
    }
}
