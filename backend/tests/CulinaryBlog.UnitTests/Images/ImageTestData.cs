using System.Text;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>Byte header giả cho từng định dạng — đủ để kiểm tra magic bytes (D24).</summary>
internal static class ImageTestData
{
    public const long FiveMegabytes = 5 * 1024 * 1024;

    public static byte[] Jpeg() => Pad([0xFF, 0xD8, 0xFF, 0xE0]);

    public static byte[] Png() => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    public static byte[] WebP() => Pad([.. Encoding.ASCII.GetBytes("RIFF"), 0x24, 0x00, 0x00, 0x00, .. Encoding.ASCII.GetBytes("WEBP")]);

    public static byte[] Avif() => Pad([0x00, 0x00, 0x00, 0x1C, .. Encoding.ASCII.GetBytes("ftypavif")]);

    /// <summary>Header của file .exe (PE) — "MZ".</summary>
    public static byte[] Exe() => Pad([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);

    /// <summary>RIFF nhưng là WAV, không phải WebP — 4 byte đầu giống WebP.</summary>
    public static byte[] Wav() => Pad([.. Encoding.ASCII.GetBytes("RIFF"), 0x24, 0x00, 0x00, 0x00, .. Encoding.ASCII.GetBytes("WAVE")]);

    private static byte[] Pad(byte[] header)
    {
        var bytes = new byte[Math.Max(header.Length, 64)];
        header.CopyTo(bytes, 0);
        return bytes;
    }
}

/// <summary>Stream ném lỗi nếu bị đọc — chứng minh size được kiểm tra TRƯỚC khi đọc stream (NFR-SEC-004).</summary>
internal sealed class ThrowOnReadStream : Stream
{
    public bool WasRead { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => throw new InvalidOperationException("Không được truy cập Length của stream.");
    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        WasRead = true;
        throw new InvalidOperationException("Stream bị đọc trước khi kiểm tra size.");
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
