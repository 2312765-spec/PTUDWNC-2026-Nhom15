namespace CulinaryBlog.Domain.Exceptions;

/// <summary>
/// Exception cho vi phạm business rule trong tầng Domain (vd: Recipe.Publish() thiếu step).
/// Map sang 400 VALIDATION_ERROR-style ở GlobalExceptionMiddleware (D4), trừ khi ErrorCode
/// khớp một Application Error Code cụ thể hơn (vd: RECIPE_PRIMARY_IMAGE_REQUIRED).
/// </summary>
public class DomainException : Exception
{
    public string ErrorCode { get; set; } = string.Empty;

    public DomainException()
    {
    }

    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
