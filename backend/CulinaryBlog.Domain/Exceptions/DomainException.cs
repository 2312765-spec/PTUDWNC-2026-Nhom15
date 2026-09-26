namespace CulinaryBlog.Domain.Exceptions
{
    public class DomainException : Exception
    {
        public DomainException() { }
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}

namespace CulinaryBlog.Domain
{
    // Alias để các file test without 'using CulinaryBlog.Domain.Exceptions' vẫn tìm thấy trực tiếp
    public class DomainException : CulinaryBlog.Domain.Exceptions.DomainException
    {
        public DomainException() { }
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }
}