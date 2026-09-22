using System.Net;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidationException = FluentValidation.ValidationException;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// CONS-005 — mọi lỗi trả về theo RFC 7807 (application/problem+json).
///
/// Bảng map dưới đây là hiện thực của quyết định D4 trong docs/decisions.md.
/// LƯU Ý: SRS Chương 3 dùng 422 ở nhiều chỗ — SAI. Hệ thống KHÔNG dùng 422 ở bất kỳ đâu.
///
/// | Exception                        | HTTP | Error code                    |
/// |----------------------------------|------|-------------------------------|
/// | FluentValidation.ValidationException | 400 | VALIDATION_ERROR           |
/// | DomainException                  | 400  | theo từng rule                |
/// | UnauthorizedException            | 401  | AUTH_TOKEN_INVALID            |
/// | ForbiddenException               | 403  | RECIPE_FORBIDDEN…             |
/// | NotFoundException                | 404  | *_NOT_FOUND                   |
/// | ConflictException                | 409  | *_EXISTS…                     |
/// | DbUpdateConcurrencyException     | 409  | RECIPE_CONCURRENCY_CONFLICT   |
/// | LockedException                  | 423  | AUTH_ACCOUNT_LOCKED           |
/// | còn lại                          | 500  | — (không lộ stack trace)      |
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, errorCode, title, errors) = Map(exception);

        if (status >= 500)
        {
            logger.LogError(exception, "Lỗi chưa xử lý: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning("{ErrorCode} ({Status}): {Message}", errorCode, status, exception.Message);
        }

        var problem = new ProblemDetails
        {
            // RFC 7807 "type" mang Application Error Code để frontend xử lý theo mã,
            // không phụ thuộc chuỗi message (NFR-USE-003).
            Type = errorCode,
            Title = title,
            Status = status,
            Detail = status >= 500 && !environment.IsDevelopment()
                ? "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."
                : exception.Message,
            Instance = context.Request.Path,
        };

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static (int Status, string ErrorCode, string Title, IDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            // D30: nếu mọi lỗi cùng mang một ErrorCode riêng (vd FILE_SIZE_EXCEEDED) thì dùng
            // mã đó làm "type" — lỗi hỗn hợp nhiều field/mã khác nhau vẫn fallback VALIDATION_ERROR.
            FluentValidationException ve => (
                (int)HttpStatusCode.BadRequest,
                SingleSharedErrorCode(ve) ?? ErrorCodes.ValidationError,
                "Dữ liệu không hợp lệ",
                ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            DomainException de => (
                (int)HttpStatusCode.BadRequest,
                de.ErrorCode,
                "Vi phạm quy tắc nghiệp vụ",
                null),

            UnauthorizedException ue => ((int)HttpStatusCode.Unauthorized, ue.ErrorCode, "Chưa xác thực", null),
            ForbiddenException fe => ((int)HttpStatusCode.Forbidden, fe.ErrorCode, "Không có quyền", null),
            NotFoundException nfe => ((int)HttpStatusCode.NotFound, nfe.ErrorCode, "Không tìm thấy", null),
            ConflictException ce => ((int)HttpStatusCode.Conflict, ce.ErrorCode, "Xung đột dữ liệu", null),
            LockedException le => (423, le.ErrorCode, "Tài khoản bị khóa", null),

            // D4: concurrency conflict là 409, KHÔNG phải 422 như Phụ lục A/B của SRS ghi.
            DbUpdateConcurrencyException => (
                (int)HttpStatusCode.Conflict,
                ErrorCodes.RecipeConcurrencyConflict,
                "Dữ liệu đã bị thay đổi bởi người dùng khác",
                null),

            _ => ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "Lỗi hệ thống", null),
        };

    /// <summary>
    /// D30 — FluentValidation tự gán ErrorCode mặc định = TÊN VALIDATOR (vd "NotEmptyValidator")
    /// cho mọi rule không gọi .WithErrorCode(...)/không tự set ErrorCode — "khác rỗng" không đủ
    /// để nhận biết một Application Error Code thật. Chỉ coi là Application Error Code khi khớp
    /// đúng quy ước SCREAMING_SNAKE_CASE (docs/CLAUDE.md mục 6) VÀ tất cả lỗi dùng chung mã đó.
    /// </summary>
    private static readonly Regex ApplicationErrorCodePattern = new("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.Compiled);

    private static string? SingleSharedErrorCode(FluentValidationException ve)
    {
        var codes = ve.Errors
            .Select(e => e.ErrorCode)
            .Distinct()
            .ToList();

        return codes is [var code] && ApplicationErrorCodePattern.IsMatch(code) ? code : null;
    }
}
