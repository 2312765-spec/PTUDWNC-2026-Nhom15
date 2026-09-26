using System.Net;
using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Common;
using CulinaryBlog.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidationException = FluentValidation.ValidationException;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// CONS-005 — Mọi lỗi trả về theo RFC 7807 (application/problem+json).
/// Quyết định D4: 400 cho validation, 401 cho auth, 403 cho forbidden, 404 cho not found, 409 cho conflict, 423 cho locked.
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
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response đã bắt đầu gửi, không thể ghi đè ProblemDetails.");
            return;
        }

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
            Type = errorCode ?? "INTERNAL_SERVER_ERROR",
            Title = title ?? "Lỗi hệ thống",
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
            // BadRequestException
            BadRequestException bre => (
                StatusCodes.Status400BadRequest,
                bre.ErrorCode,
                "Dữ liệu không hợp lệ",
                null),

            

            // DomainException từ Domain
            DomainException de => (
                StatusCodes.Status400BadRequest,
                string.IsNullOrEmpty(de.ErrorCode) ? ErrorCodes.RecipePrimaryImageRequired : de.ErrorCode,
                "Vi phạm quy tắc nghiệp vụ",
                null),

            // D22/D27: Bắt InvalidOperationException liên quan đến Primary Image
            InvalidOperationException ioe when ioe.Message.Contains("RECIPE_PRIMARY_IMAGE_REQUIRED") || ioe.Message.Contains("primary") => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.RecipePrimaryImageRequired,
                "Vi phạm quy tắc ảnh chính",
                null),

            // FluentValidation
            FluentValidationException fve => (
                StatusCodes.Status400BadRequest,
                SingleSharedErrorCode(fve) ?? ErrorCodes.ValidationError,
                "Dữ liệu không hợp lệ",
                fve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            // Sai credentials -> 401 AUTH_INVALID_CREDENTIALS
            UnauthorizedException ue => (
                StatusCodes.Status401Unauthorized, 
                string.IsNullOrEmpty(ue.ErrorCode) ? "AUTH_INVALID_CREDENTIALS" : ue.ErrorCode, 
                "Chưa xác thực", 
                null),

            // Không có quyền -> 403
            ForbiddenException fe => (
                StatusCodes.Status403Forbidden, 
                string.IsNullOrEmpty(fe.ErrorCode) ? "FORBIDDEN" : fe.ErrorCode, 
                "Không có quyền", 
                null),

            // Không tìm thấy -> 404
            NotFoundException nfe => (
                StatusCodes.Status404NotFound, 
                string.IsNullOrEmpty(nfe.ErrorCode) ? "NOT_FOUND" : nfe.ErrorCode, 
                "Không tìm thấy", 
                null),

            // Conflict / Duplicate -> 409
            ConflictException ce => (
                StatusCodes.Status409Conflict, 
                string.IsNullOrEmpty(ce.ErrorCode) ? "CONFLICT" : ce.ErrorCode, 
                "Xung đột dữ liệu", 
                null),

            // Khóa tài khoản -> 423 AUTH_ACCOUNT_LOCKED (D17)
            LockedException le => (
                StatusCodes.Status423Locked, 
                string.IsNullOrEmpty(le.ErrorCode) ? "AUTH_ACCOUNT_LOCKED" : le.ErrorCode, 
                "Tài khoản bị khóa", 
                null),

            // Concurrency conflict -> 409 (D4)
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                ErrorCodes.RecipeConcurrencyConflict,
                "Dữ liệu đã bị thay đổi bởi người dùng khác",
                null),

            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "Lỗi hệ thống", null),
        };

    private static readonly Regex _applicationErrorCodePattern = new("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.Compiled);

    private static string? SingleSharedErrorCode(FluentValidationException ve)
    {
        var codes = ve.Errors
            .Select(e => e.ErrorCode)
            .Distinct()
            .ToList();

        return codes is [var code] && _applicationErrorCodePattern.IsMatch(code) ? code : null;
    }
}