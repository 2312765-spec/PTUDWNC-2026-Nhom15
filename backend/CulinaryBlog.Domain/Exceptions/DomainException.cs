namespace CulinaryBlog.Domain.Exceptions
{
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
}

namespace CulinaryBlog.Domain.Entities
{
    public class DomainException : CulinaryBlog.Domain.Exceptions.DomainException
    {
        public DomainException() { }
        public DomainException(string message) : base(message) { }
        public DomainException(string message, string errorCode) : base(message, errorCode) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}