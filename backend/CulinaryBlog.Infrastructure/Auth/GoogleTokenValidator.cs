using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Auth;

/// <summary>
/// FR-AUTH-003, D9 — verify Google ID Token qua thư viện chính thức Google.Apis.Auth.
/// D33: lỗi xác thực token (invalid/hết hạn/sai audience — SRS A1+A2 gộp) → BadRequestException 400.
/// Lỗi gọi Google thất bại vì hạ tầng (SRS A3) → BadGatewayException 502.
/// </summary>
public sealed class GoogleTokenValidator(IConfiguration configuration) : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var clientId = configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Thiếu cấu hình Google:ClientId.");

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId],
            });
        }
        catch (InvalidJwtException)
        {
            throw new BadRequestException(ErrorCodes.AuthGoogleTokenInvalid, "Token Google không hợp lệ hoặc đã hết hạn.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BadGatewayException(ErrorCodes.AuthGoogleUnavailable, "Không thể xác thực với Google lúc này.");
        }

        return new GoogleUserInfo(payload.Email, payload.Name, payload.Picture, payload.Subject, payload.EmailVerified);
    }
}
