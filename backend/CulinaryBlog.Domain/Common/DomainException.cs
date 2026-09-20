namespace CulinaryBlog.Domain.Common;

/// <summary>
/// Vi phạm business rule của tầng Domain.
/// GlobalExceptionMiddleware map sang HTTP 400 kèm ErrorCode (D4).
/// </summary>
public class DomainException(string errorCode, string message) : Exception(message)
{
    /// <summary>Application Error Code dạng SCREAMING_SNAKE_CASE — xem decisions.md.</summary>
    public string ErrorCode { get; } = errorCode;
}
