using CulinaryBlog.Application.Auth.Commands.UpdateProfile;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace CulinaryBlog.UnitTests.Auth;

/// <summary>FR-AUTH-007 — ràng buộc của UpdateProfileCommandValidator (D5, D51; CONS-008).</summary>
public class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    [Fact(DisplayName = "FR-AUTH-007: cả 3 field hợp lệ → pass")]
    public void AllFieldsValid_Passes()
    {
        var result = _validator.TestValidate(new UpdateProfileCommand("Bếp Trưởng", "https://example.com/a.png", "Yêu ẩm thực"));

        result.IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-AUTH-007/D51-1: chỉ cần một field có mặt là đủ (PATCH một phần)")]
    [InlineData("Bếp Trưởng", null, null)]
    [InlineData(null, "https://example.com/a.png", null)]
    [InlineData(null, null, "Bio")]
    public void SingleField_Passes(string? displayName, string? avatarUrl, string? bio)
    {
        _validator.TestValidate(new UpdateProfileCommand(displayName, avatarUrl, bio)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-AUTH-007/D51-1: không có field nào → lỗi")]
    public void NoFields_Fails()
    {
        var result = _validator.TestValidate(new UpdateProfileCommand(null, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("ít nhất một");
    }

    [Theory(DisplayName = "FR-AUTH-007/D5,D51-3: displayName rỗng, khoảng trắng hoặc 1 ký tự (sau trim) → lỗi")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("  A  ")]
    public void DisplayNameTooShort_Fails(string displayName)
    {
        var result = _validator.TestValidate(new UpdateProfileCommand(displayName, null, null));

        result.ShouldHaveAnyValidationError();
    }

    [Theory(DisplayName = "FR-AUTH-007/D5: displayName đúng biên 2 và 100 ký tự → pass")]
    [InlineData(2)]
    [InlineData(100)]
    public void DisplayNameAtBoundary_Passes(int length)
    {
        _validator.TestValidate(new UpdateProfileCommand(new string('a', length), null, null)).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: displayName 101 ký tự → lỗi")]
    public void DisplayNameTooLong_Fails()
    {
        _validator.TestValidate(new UpdateProfileCommand(new string('a', 101), null, null)).ShouldHaveAnyValidationError();
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: khoảng trắng hai đầu không tính vào độ dài displayName")]
    public void DisplayNameLengthIgnoresSurroundingWhitespace_Passes()
    {
        _validator.TestValidate(new UpdateProfileCommand("  " + new string('a', 100) + "  ", null, null)).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-AUTH-007/D51-2: avatarUrl rỗng là lệnh xóa → hợp lệ")]
    [InlineData("")]
    public void EmptyAvatarUrl_IsClearCommand_Passes(string avatarUrl)
    {
        _validator.TestValidate(new UpdateProfileCommand(null, avatarUrl, null)).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-AUTH-007/D5,D51-3: avatarUrl http/https tuyệt đối → pass")]
    [InlineData("http://example.com/a.png")]
    [InlineData("https://example.com/a.png?x=1")]
    public void AbsoluteHttpUrl_Passes(string avatarUrl)
    {
        _validator.TestValidate(new UpdateProfileCommand(null, avatarUrl, null)).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "FR-AUTH-007/D51-3: avatarUrl không phải URL http/https tuyệt đối → lỗi (kể cả javascript:)")]
    [InlineData("not-a-url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("/relative/path.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public void NotHttpUrl_Fails(string avatarUrl)
    {
        _validator.TestValidate(new UpdateProfileCommand(null, avatarUrl, null)).ShouldHaveAnyValidationError();
    }

    [Fact(DisplayName = "FR-AUTH-007: avatarUrl vượt 500 ký tự → lỗi, đúng 500 → pass")]
    public void AvatarUrlLength_Boundary()
    {
        var prefix = "https://example.com/";

        _validator.TestValidate(new UpdateProfileCommand(null, prefix + new string('a', 500 - prefix.Length), null))
            .IsValid.Should().BeTrue();
        _validator.TestValidate(new UpdateProfileCommand(null, prefix + new string('a', 501 - prefix.Length), null))
            .ShouldHaveAnyValidationError();
    }

    [Fact(DisplayName = "FR-AUTH-007/D5: bio đúng 1000 ký tự → pass, 1001 → lỗi")]
    public void BioLength_Boundary()
    {
        _validator.TestValidate(new UpdateProfileCommand(null, null, new string('b', 1000))).IsValid.Should().BeTrue();
        _validator.TestValidate(new UpdateProfileCommand(null, null, new string('b', 1001))).ShouldHaveAnyValidationError();
    }

    [Fact(DisplayName = "FR-AUTH-007/D51-2: bio rỗng là lệnh xóa → hợp lệ")]
    public void EmptyBio_IsClearCommand_Passes()
    {
        _validator.TestValidate(new UpdateProfileCommand(null, null, "")).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-AUTH-007/D4: một field sai làm cả lệnh fail dù field khác hợp lệ")]
    public void OneInvalidField_FailsWholeCommand()
    {
        var result = _validator.TestValidate(new UpdateProfileCommand("Tên hợp lệ", "not-a-url", "bio"));

        result.IsValid.Should().BeFalse();
    }
}
