using System.Text;

namespace CulinaryBlog.Application.Common.Files;

/// <summary>FR-FILE-001/D28 — kết quả nhận diện định dạng ảnh bằng magic bytes.</summary>
public sealed record ImageSignatureResult(string ContentType, string Extension);

/// <summary>
/// FR-FILE-001/CONS-007/D28 — nhận diện định dạng ảnh thật bằng magic bytes, không tin
/// Content-Type header hay đuôi file client gửi lên (NFR-SEC-004).
/// </summary>
public static class ImageSignature
{
    public static ImageSignatureResult? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new ImageSignatureResult("image/jpeg", ".jpg");
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return new ImageSignatureResult("image/png", ".png");
        }

        // WebP: "RIFF" + 4 byte size + "WEBP". Kiểm cả "WEBP" để không nhận nhầm WAV/AVI (cũng RIFF).
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return new ImageSignatureResult("image/webp", ".webp");
        }

        // AVIF/HEIF container: 4 byte size + "ftyp" + brand (offset 8). Chỉ nhận brand avif/avis.
        if (header.Length >= 12 &&
            header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
        {
            var brand = Encoding.ASCII.GetString(header.Slice(8, 4));
            if (brand is "avif" or "avis")
            {
                return new ImageSignatureResult("image/avif", ".avif");
            }
        }

        return null;
    }
}
