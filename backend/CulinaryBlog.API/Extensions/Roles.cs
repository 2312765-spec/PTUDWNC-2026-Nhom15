namespace CulinaryBlog.API.Extensions;

/// <summary>SRS mục 2.3 — 3 vai trò. Guest không phải role, chỉ là "chưa đăng nhập".</summary>
public static class Roles
{
    public const string Author = "Author";
    public const string Admin = "Admin";
}

/// <summary>
/// NFR-SEC-006 — dùng policy, KHÔNG hardcode chuỗi role trong endpoint.
/// D12: policy "VerifiedAuthor" đã bỏ khỏi v1, chỉ còn 2 policy.
/// </summary>
public static class Policies
{
    public const string Author = "AuthorPolicy";
    public const string Admin = "AdminPolicy";
}
