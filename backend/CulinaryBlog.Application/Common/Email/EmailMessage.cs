namespace CulinaryBlog.Application.Common.Email;

/// <summary>
/// FR-JOB-001/D57 — một email đã dựng xong nội dung. Application dựng, <c>IEmailService</c> ở
/// Infrastructure chỉ gửi (multipart/alternative: HTML + plain text).
/// </summary>
public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);
