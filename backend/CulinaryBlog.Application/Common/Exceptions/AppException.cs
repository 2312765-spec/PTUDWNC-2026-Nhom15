namespace CulinaryBlog.Application.Common.Exceptions;

/// <summary>
/// Lớp cơ sở cho exception của tầng Application.
/// Mỗi lớp con map sang một HTTP status cố định — xem decisions.md D4.
/// </summary>
public abstract class AppException(string errorCode, string message) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>404 — tài nguyên không tồn tại hoặc đã soft-delete.</summary>
public sealed class NotFoundException(string errorCode, string message)
    : AppException(errorCode, message);

/// <summary>409 — trùng unique field, hoặc concurrency conflict (D4).</summary>
public sealed class ConflictException(string errorCode, string message)
    : AppException(errorCode, message);

/// <summary>403 — đã xác thực nhưng không đủ quyền.</summary>
public sealed class ForbiddenException(string errorCode, string message)
    : AppException(errorCode, message);

/// <summary>401 — chưa xác thực hoặc token không hợp lệ.</summary>
public sealed class UnauthorizedException(string errorCode, string message)
    : AppException(errorCode, message);

/// <summary>423 — tài khoản bị khóa tạm thời (D17).</summary>
public sealed class LockedException(string errorCode, string message)
    : AppException(errorCode, message);

/// <summary>400 — dữ liệu không hợp lệ hoặc vi phạm nghiệp vụ (D4).</summary>
public sealed class BadRequestException(string errorCode, string message)
    : AppException(errorCode, message);
    