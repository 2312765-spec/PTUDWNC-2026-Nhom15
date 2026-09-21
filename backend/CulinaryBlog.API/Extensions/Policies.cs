namespace CulinaryBlog.API.Extensions;

/// <summary>
/// NFR-SEC-006 — dùng policy, KHÔNG hardcode chuỗi role trong endpoint.
/// D12: policy "VerifiedAuthor" đã bỏ khỏi v1, chỉ còn 2 policy.
/// </summary>
public static class Policies
{
    public const string Author = "AuthorPolicy";
    public const string Admin = "AdminPolicy";
}
