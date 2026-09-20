namespace CulinaryBlog.Infrastructure.Caching;

/// <summary>
/// D8 — bảng cache key và TTL. Nguồn duy nhất, đừng hardcode chuỗi ở nơi khác.
/// </summary>
public static class CacheKeys
{
    public static class Tags
    {
        public const string Categories = "categories";
        public const string Recipes = "recipes";

        public static string Recipe(string slug) => $"recipe:{slug}";
    }

    public const string CategoriesAll = "categories:all";

    public static string RecipeDetail(string slug) => $"recipe:{slug}";
    public static string RecipeList(string queryHash) => $"recipes:list:{queryHash}";
    public static string RecipeSearch(string queryHash) => $"recipes:search:{queryHash}";

    public static class Ttl
    {
        public static readonly TimeSpan Categories = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan RecipeList = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan RecipeDetail = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Search = TimeSpan.FromMinutes(1);
    }
}
