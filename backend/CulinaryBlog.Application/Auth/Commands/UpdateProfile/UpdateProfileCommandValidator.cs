using FluentValidation;

namespace CulinaryBlog.Application.Auth.Commands.UpdateProfile;

/// <summary>
/// CONS-008 — validate CHỈ ở đây, chạy trong ValidationBehavior. Ràng buộc theo D5 và D54:
/// ít nhất một field; displayName 2–100 (sau trim); avatarUrl là URL http/https tuyệt đối;
/// bio ≤ 1000. Chuỗi rỗng ở avatarUrl/bio là lệnh xóa nên luôn hợp lệ.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 100;
    public const int AvatarUrlMaxLength = 500;
    public const int BioMaxLength = 1000;

    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.DisplayName is not null || x.AvatarUrl is not null || x.Bio is not null)
            .WithName("body")
            .WithMessage("Cần cung cấp ít nhất một trong displayName, avatarUrl, bio.");

        When(x => x.DisplayName is not null, () =>
            RuleFor(x => x.DisplayName!.Trim())
                .Length(DisplayNameMinLength, DisplayNameMaxLength)
                .WithName("displayName"));

        When(x => !string.IsNullOrEmpty(x.AvatarUrl), () =>
            RuleFor(x => x.AvatarUrl!)
                .MaximumLength(AvatarUrlMaxLength)
                .Must(IsAbsoluteHttpUrl).WithMessage("'avatarUrl' phải là URL http hoặc https hợp lệ.")
                .WithName("avatarUrl"));

        When(x => x.Bio is not null, () =>
            RuleFor(x => x.Bio!)
                .MaximumLength(BioMaxLength)
                .WithName("bio"));
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
