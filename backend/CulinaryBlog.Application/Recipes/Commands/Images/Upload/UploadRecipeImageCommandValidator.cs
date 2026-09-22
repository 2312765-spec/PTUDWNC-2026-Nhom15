using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Files;
using FluentValidation;
using FluentValidation.Results;

namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>
/// FR-RCP-008/CONS-007/CONS-008/NFR-SEC-004 — thứ tự bắt buộc: size (KHÔNG đọc stream) → MIME
/// declared → magic bytes. Đây là nơi DUY NHẤT làm validation (CONS-008 — không validate trong
/// endpoint handler).
/// </summary>
public sealed class UploadRecipeImageCommandValidator : AbstractValidator<UploadRecipeImageCommand>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private const int HeaderProbeSize = 32;

    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/avif"];

    public UploadRecipeImageCommandValidator()
    {
        RuleFor(x => x.AltText).MaximumLength(200);

        RuleFor(x => x).CustomAsync(ValidateFileAsync);
    }

    private static async Task ValidateFileAsync(
        UploadRecipeImageCommand command, ValidationContext<UploadRecipeImageCommand> context, CancellationToken ct)
    {
        // 1. Size TRƯỚC — không được đọc stream nếu đã quá giới hạn (chống DoS, NFR-SEC-004).
        if (command.Length > MaxFileSizeBytes)
        {
            context.AddFailure(new ValidationFailure(nameof(UploadRecipeImageCommand.Content), "Kích thước file vượt quá giới hạn 5MB.")
            {
                ErrorCode = ErrorCodes.FileSizeExceeded,
            });
            return;
        }

        // 2. MIME type client khai báo.
        if (!AllowedContentTypes.Contains(command.ContentType))
        {
            context.AddFailure(new ValidationFailure(nameof(UploadRecipeImageCommand.ContentType), "Định dạng ảnh không được hỗ trợ.")
            {
                ErrorCode = ErrorCodes.FileMimeInvalid,
            });
            return;
        }

        // 3. Magic bytes — đọc rồi luôn tua stream về đầu để handler upload đủ nội dung.
        var header = new byte[HeaderProbeSize];
        var bytesRead = await ReadHeaderAsync(command.Content, header, ct);
        command.Content.Position = 0;

        var signature = ImageSignature.Detect(header.AsSpan(0, bytesRead));
        if (signature is null || signature.ContentType != command.ContentType)
        {
            context.AddFailure(new ValidationFailure(nameof(UploadRecipeImageCommand.Content), "File không hợp lệ.")
            {
                ErrorCode = ErrorCodes.FileMimeInvalid,
            });
        }
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct)) > 0)
        {
            totalRead += read;
        }

        return totalRead;
    }
}
