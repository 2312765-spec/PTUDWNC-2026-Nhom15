using System.Text;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Infrastructure.Files;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Files;

/// <summary>
/// FR-JOB-002 — D40 (crop/fit, không upscale, auto-orient, bỏ EXIF, giới hạn điểm ảnh) và D41
/// (WebP, AVIF → null). Đặt ở IntegrationTests vì UnitTests chỉ reference Application, còn
/// <see cref="ImageSharpImageResizer"/> nằm ở Infrastructure. Không cần container.
/// </summary>
public sealed class ImageSharpImageResizerTests
{
    private readonly ImageSharpImageResizer _sut = new();

    [Theory(DisplayName = "FR-JOB-002/D40: thumbnail crop đúng 300×300, medium fit trong 800×600 giữ tỉ lệ")]
    [InlineData(1200, 900, 800, 600)]
    [InlineData(2000, 500, 800, 200)]
    [InlineData(600, 1200, 300, 600)]
    public void Resize_LargeImage_ThumbnailCroppedMediumFitted(int width, int height, int mediumWidth, int mediumHeight)
    {
        using var source = new MemoryStream(Encode(width, height, png: true));

        var result = _sut.Resize(source);

        result.Should().NotBeNull();
        Size(result!.Thumbnail).Should().Be((300, 300));
        Size(result.Medium).Should().Be((mediumWidth, mediumHeight));
    }

    [Fact(DisplayName = "FR-JOB-002/D40: ảnh nhỏ hơn đích → không upscale (medium giữ nguyên, thumbnail crop vuông theo cạnh ngắn)")]
    public void Resize_SmallImage_DoesNotUpscale()
    {
        using var source = new MemoryStream(Encode(200, 100, png: true));

        var result = _sut.Resize(source)!;

        Size(result.Medium).Should().Be((200, 100));
        Size(result.Thumbnail).Should().Be((100, 100));
    }

    [Fact(DisplayName = "FR-JOB-002/D41: đầu ra luôn là WebP (magic bytes), kể cả khi gốc là JPEG")]
    public void Resize_JpegSource_OutputsWebP()
    {
        using var source = new MemoryStream(Encode(1000, 800, png: false));

        var result = _sut.Resize(source)!;

        ImageSignature.Detect(result.Medium)!.ContentType.Should().Be("image/webp");
        ImageSignature.Detect(result.Thumbnail)!.ContentType.Should().Be("image/webp");
    }

    [Fact(DisplayName = "FR-JOB-002/D40: xoay theo EXIF Orientation trước khi resize, rồi bỏ metadata EXIF")]
    public void Resize_ExifRotated_AutoOrientsAndStripsExif()
    {
        // Ảnh lưu 1200×600 nhưng EXIF Orientation = 6 (xoay 90°) → hiển thị thật là 600×1200.
        using var image = new Image<Rgba32>(1200, 600);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        image.Metadata.ExifProfile.SetValue(ExifTag.Make, "Camera test");
        using var source = new MemoryStream();
        image.SaveAsJpeg(source);
        source.Position = 0;

        var result = _sut.Resize(source)!;

        Size(result.Medium).Should().Be((300, 600));
        Image.Identify(result.Medium).Metadata.ExifProfile.Should().BeNull();
        Image.Identify(result.Thumbnail).Metadata.ExifProfile.Should().BeNull();
    }

    [Fact(DisplayName = "FR-JOB-002/D41: ảnh AVIF (ImageSharp không decode được) → null, không ném lỗi")]
    public void Resize_Avif_ReturnsNull()
    {
        var bytes = new byte[64];
        byte[] header = [0x00, 0x00, 0x00, 0x1C, .. Encoding.ASCII.GetBytes("ftypavif")];
        header.CopyTo(bytes, 0);
        using var source = new MemoryStream(bytes);

        _sut.Resize(source).Should().BeNull();
    }

    [Fact(DisplayName = "FR-JOB-002/D40: ảnh vượt giới hạn điểm ảnh → null (chống decompression bomb), không decode")]
    public void Resize_OverPixelLimit_ReturnsNull()
    {
        var sut = new ImageSharpImageResizer(maxPixels: 100 * 100 - 1);
        using var source = new MemoryStream(Encode(100, 100, png: true));

        sut.Resize(source).Should().BeNull();
    }

    private static byte[] Encode(int width, int height, bool png)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(30, 160, 90));
        using var stream = new MemoryStream();
        if (png)
        {
            image.SaveAsPng(stream);
        }
        else
        {
            image.SaveAsJpeg(stream);
        }

        return stream.ToArray();
    }

    private static (int Width, int Height) Size(byte[] encoded)
    {
        var info = Image.Identify(encoded);
        return (info.Width, info.Height);
    }
}
