namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>
/// FR-AUTH-003, D9 — xác thực Google ID Token. Tách biệt khỏi <see cref="IIdentityService"/>
/// vì đây là việc gọi RA NGOÀI (Google), không phải nghiệp vụ Identity nội bộ.
/// </summary>
public interface IGoogleTokenValidator
{
    /// <summary>
    /// Xác thực <paramref name="idToken"/> với Google.
    /// D33: mọi lỗi xác thực token (invalid, hết hạn, sai audience, bị revoke — SRS A1+A2 gộp
    /// làm một vì ID Token không phân biệt được hai trường hợp này) → ném
    /// <see cref="CulinaryBlog.Application.Common.Exceptions.BadRequestException"/> với
    /// <c>ErrorCodes.AuthGoogleTokenInvalid</c> (400).
    /// Lỗi gọi Google thất bại vì hạ tầng (timeout, DNS, Google trả 5xx — SRS A3) → ném
    /// <c>BadGatewayException</c> với <c>ErrorCodes.AuthGoogleUnavailable</c> (502).
    /// </summary>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>Thông tin profile Google sau khi verify ID Token thành công (D9).</summary>
public sealed record GoogleUserInfo(string Email, string Name, string? Picture, string ProviderKey);
