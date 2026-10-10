using System.Text;
using SkiaSharp;

namespace CulinaryBlog.IntegrationTests.Files;

/// <summary>FR-JOB-002 — tạo và đọc ảnh thật cho test resize (SkiaSharp, D46).</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>JPEG kèm segment EXIF (APP1) mang Orientation và Make — Skia không ghi được EXIF nên chèn tay.</summary>
    public static byte[] JpegWithExif(int width, int height, ushort orientation, string make)
    {
        var jpeg = Jpeg(width, height);
        var app1 = ExifApp1(orientation, make);
        // Chèn ngay sau SOI (FF D8).
        return [.. jpeg.AsSpan(0, 2), .. app1, .. jpeg.AsSpan(2)];
    }

    /// <summary>Header AVIF hợp lệ (D28 nhận) nhưng Skia không có decoder AVIF (D41).</summary>
    public static byte[] AvifHeaderOnly()
    {
        var bytes = new byte[64];
        byte[] header = [0x00, 0x00, 0x00, 0x1C, .. Encoding.ASCII.GetBytes("ftypavif")];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    public static (int Width, int Height) Size(byte[] encoded)
    {
        using var data = SKData.CreateCopy(encoded);
        using var codec = SKCodec.Create(data) ?? throw new InvalidOperationException("Không đọc được ảnh.");
        return (codec.Info.Width, codec.Info.Height);
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(30, 160, 90));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>APP1 "Exif" với TIFF big-endian, IFD0 gồm Make (0x010F) và Orientation (0x0112).</summary>
    private static byte[] ExifApp1(ushort orientation, string make)
    {
        var makeBytes = Encoding.ASCII.GetBytes(make + "\0");
        const int ifdOffset = 8;
        const int entryCount = 2;
        const int dataOffset = ifdOffset + 2 + (entryCount * 12) + 4;

        using var tiff = new MemoryStream();
        tiff.Write("MM"u8);
        WriteUInt16(tiff, 0x002A);
        WriteUInt32(tiff, ifdOffset);
        WriteUInt16(tiff, entryCount);
        // Make: ASCII (2), độ dài makeBytes, dữ liệu nằm sau IFD.
        WriteUInt16(tiff, 0x010F);
        WriteUInt16(tiff, 2);
        WriteUInt32(tiff, (uint)makeBytes.Length);
        WriteUInt32(tiff, dataOffset);
        // Orientation: SHORT (3), 1 giá trị, nằm ngay trong ô value (căn trái).
        WriteUInt16(tiff, 0x0112);
        WriteUInt16(tiff, 3);
        WriteUInt32(tiff, 1);
        WriteUInt16(tiff, orientation);
        WriteUInt16(tiff, 0);
        WriteUInt32(tiff, 0); // không có IFD kế tiếp
        tiff.Write(makeBytes);

        byte[] payload = [.. "Exif\0\0"u8, .. tiff.ToArray()];
        var length = payload.Length + 2;
        return [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload];
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        WriteUInt16(stream, (ushort)(value >> 16));
        WriteUInt16(stream, (ushort)value);
    }
}
