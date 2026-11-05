namespace CulinaryBlog.API.Middleware;

/// <summary>
/// NFR-SEC-004/D68 — Content-Security-Policy cho mọi response API. API chỉ trả JSON nên không cần tải
/// script/ảnh/khung nào: chặn hết. Gắn lúc response bắt đầu gửi (OnStarting) để có cả trên response lỗi
/// do GlobalExceptionMiddleware ghi. Nginx KHÔNG thêm CSP — đúng một header mỗi response.
/// Bỏ qua <c>/scalar</c>: trang HTML tài liệu API (chỉ Development) cần script từ CDN.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/scalar"))
        {
            return next(context);
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
            return Task.CompletedTask;
        });

        return next(context);
    }
}
