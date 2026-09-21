using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Commands.Images;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>
/// FR-RCP-008 / CONS-007 / CONS-008 / NFR-SEC-004 — 4 tình huống tấn công upload.
/// Thứ tự kiểm tra: size (không đọc stream) → MIME → magic bytes.
/// </summary>
public class UploadRecipeImageCommandValidatorTests
{
    private readonly UploadRecipeImageCommandValidator _validator = new();

    private static UploadRecipeImageCommand Command(
        byte[] bytes, string contentType, string fileName = "pho.jpg", string? altText = null, long? length = null) =>
        new(Guid.NewGuid(), new MemoryStream(bytes), length ?? bytes.Length, contentType, fileName, altText);

    [Theory(DisplayName = "FR-RCP-008/D24: JPEG, PNG, WebP, AVIF hợp lệ đi qua")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/avif")]
    public async Task ValidImage_Passes(string contentType)
    {
        var bytes = contentType switch
        {
            "image/jpeg" => ImageTestData.Jpeg(),
            "image/png" => ImageTestData.Png(),
            "image/webp" => ImageTestData.WebP(),
            _ => ImageTestData.Avif(),
        };

        var result = await _validator.ValidateAsync(Command(bytes, contentType));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008: validator trả stream về đầu để handler upload đủ nội dung")]
    public async Task Validation_RewindsStream()
    {
        var command = Command(ImageTestData.Jpeg(), "image/jpeg");

        await _validator.ValidateAsync(command);

        command.Content.Position.Should().Be(0);
    }

    [Fact(DisplayName = "FR-RCP-008/CONS-007: đúng 5 MB vẫn hợp lệ (biên)")]
    public async Task ExactlyFiveMegabytes_Passes()
    {
        var result = await _validator.ValidateAsync(
            Command(ImageTestData.Jpeg(), "image/jpeg", length: ImageTestData.FiveMegabytes));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008/A2: file 6 MB → FILE_SIZE_EXCEEDED")]
    public async Task SixMegabytes_FailsWithFileSizeExceeded()
    {
        var result = await _validator.ValidateAsync(
            Command(ImageTestData.Jpeg(), "image/jpeg", length: 6 * 1024 * 1024));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileSizeExceeded);
    }

    [Fact(DisplayName = "NFR-SEC-004: size được kiểm tra TRƯỚC khi đọc stream (chống DoS)")]
    public async Task Oversize_DoesNotReadStream()
    {
        var stream = new ThrowOnReadStream();
        var command = new UploadRecipeImageCommand(
            Guid.NewGuid(), stream, ImageTestData.FiveMegabytes + 1, "image/jpeg", "big.jpg", null);

        var result = await _validator.ValidateAsync(command);

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileSizeExceeded);
        stream.WasRead.Should().BeFalse();
    }

    [Theory(DisplayName = "FR-RCP-008/A1: Content-Type ngoài 4 MIME cho phép → FILE_MIME_INVALID")]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("application/x-msdownload")]
    [InlineData("")]
    public async Task DisallowedContentType_FailsWithFileMimeInvalid(string contentType)
    {
        var result = await _validator.ValidateAsync(Command(ImageTestData.Jpeg(), contentType));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileMimeInvalid);
    }

    [Fact(DisplayName = "FR-RCP-008/A3: file .exe đổi tên .jpg, khai Content-Type image/jpeg → FILE_MIME_INVALID")]
    public async Task ExeRenamedToJpg_FailsWithFileMimeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(ImageTestData.Exe(), "image/jpeg", "virus.jpg"));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileMimeInvalid);
    }

    [Fact(DisplayName = "FR-RCP-008/D24: bytes là JPEG nhưng khai Content-Type image/png (lệch) → FILE_MIME_INVALID")]
    public async Task MismatchedContentType_FailsWithFileMimeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(ImageTestData.Jpeg(), "image/png"));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileMimeInvalid);
    }

    [Fact(DisplayName = "FR-RCP-008/D24: WAV (RIFF) khai là image/webp → FILE_MIME_INVALID")]
    public async Task WavClaimedAsWebP_FailsWithFileMimeInvalid()
    {
        var result = await _validator.ValidateAsync(Command(ImageTestData.Wav(), "image/webp", "a.webp"));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileMimeInvalid);
    }

    [Fact(DisplayName = "FR-RCP-008: file rỗng → FILE_MIME_INVALID")]
    public async Task EmptyFile_Fails()
    {
        var result = await _validator.ValidateAsync(Command([], "image/jpeg"));

        result.Errors.Should().Contain(e => e.ErrorCode == ErrorCodes.FileMimeInvalid);
    }

    [Fact(DisplayName = "NFR-SEC-004: tên file chứa '../' không ảnh hưởng kết quả — chỉ nội dung quyết định")]
    public async Task PathTraversalFileName_IsIgnoredByValidation()
    {
        var result = await _validator.ValidateAsync(
            Command(ImageTestData.Jpeg(), "image/jpeg", fileName: "../../../etc/passwd.jpg"));

        result.IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "FR-RCP-008/D23: altText dài hơn 200 ký tự → VALIDATION")]
    public async Task AltTextTooLong_Fails()
    {
        var result = await _validator.ValidateAsync(
            Command(ImageTestData.Jpeg(), "image/jpeg", altText: new string('a', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadRecipeImageCommand.AltText));
    }
}
