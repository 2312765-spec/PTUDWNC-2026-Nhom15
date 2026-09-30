using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;

namespace CulinaryBlog.IntegrationTests.Common;

/// <summary>
/// Test double của <see cref="IGoogleTokenValidator"/> — thay cho gọi Google thật (không thể
/// tạo ID Token Google hợp lệ trong integration test). Format token giả:
/// "fake:google|{email}|{name}|{picture}|{providerKey}|{emailVerified 1/0}", sinh bởi <see cref="ForUser"/>.
/// D33: <see cref="InvalidToken"/> mô phỏng SRS A1+A2 (gộp), <see cref="UnavailableToken"/>
/// mô phỏng SRS A3.
/// </summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public const string InvalidToken = "test-invalid-google-token";
    public const string UnavailableToken = "test-google-unavailable";

    public static string ForUser(string email, string name, string? picture, string providerKey, bool emailVerified = true) =>
        $"fake:google|{email}|{name}|{picture}|{providerKey}|{(emailVerified ? "1" : "0")}";

    public Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (idToken == InvalidToken)
        {
            throw new BadRequestException(ErrorCodes.AuthGoogleTokenInvalid, "Token Google không hợp lệ hoặc đã hết hạn.");
        }

        if (idToken == UnavailableToken)
        {
            throw new BadGatewayException(ErrorCodes.AuthGoogleUnavailable, "Không thể xác thực với Google lúc này.");
        }

        var parts = idToken.Split('|');
        if (parts.Length != 6 || parts[0] != "fake:google")
        {
            throw new BadRequestException(ErrorCodes.AuthGoogleTokenInvalid, "Token Google không hợp lệ hoặc đã hết hạn.");
        }

        var picture = string.IsNullOrEmpty(parts[3]) ? null : parts[3];
        return Task.FromResult(new GoogleUserInfo(parts[1], parts[2], picture, parts[4], EmailVerified: parts[5] == "1"));
    }
}
