namespace CulinaryBlog.Application.Recipes.Commands.Ingredients;

/// <summary>FR-RCP-009/D7 — chuẩn hóa chuỗi trước khi giao cho domain: cắt khoảng trắng, chuỗi rỗng = null.</summary>
internal static class RecipeIngredientText
{
    public static string? OptionalTrim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
