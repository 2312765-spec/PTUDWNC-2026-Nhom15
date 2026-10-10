using System.Text;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Infrastructure.Files;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Files;

/// <summary>
/// FR-JOB-002 — D40 (crop/fit, không upscale, auto-orient, bỏ EXIF, giới hạn điểm ảnh) và D41
/// (WebP, AVIF → null). Đặt ở IntegrationTests vì UnitTests chỉ reference Application, còn
/// <see cref="SkiaImageResizer"/> nằm ở Infrastructure. Không cần container.
/// </summary>
public sealed class SkiaImageResizerTests
{
    private readonly SkiaImageResizer _sut = new();

    [Theory(DisplayName = "FR-JOB-002/D40: thumbnail crop đúng 300×300, medium fit trong 800×600 giữ tỉ lệ")]
    [InlineData(1200, 900, 800, 600)]
    [InlineData(2000, 500, 800, 200)]
    [InlineData(600, 1200, 300, 600)]
    public void Resize_LargeImage_ThumbnailCroppedMediumFitted(int width, int height, int mediumWidth, int mediumHeight)
    {
        using var source = new MemoryStream(TestImages.Png(width, height));

        var result = _sut.Resize(source);

        result.Should().NotBeNull();
        TestImages.Size(result!.Thumbnail).Should().Be((300, 300));
        TestImages.Size(result.Medium).Should().Be((mediumWidth, mediumHeight));
    }

    [Fact(DisplayName = "FR-JOB-002/D40: ảnh nhỏ hơn đích → không upscale (medium giữ nguyên, thumbnail crop vuông theo cạnh ngắn)")]
    public void Resize_SmallImage_DoesNotUpscale()
    {
        using var source = new MemoryStream(TestImages.Png(200, 100));

        var result = _sut.Resize(source)!;

        TestImages.Size(result.Medium).Should().Be((200, 100));
        TestImages.Size(result.Thumbnail).Should().Be((100, 100));
    }

    [Fact(DisplayName = "FR-JOB-002/D41: đầu ra luôn là WebP (magic bytes), kể cả khi gốc là JPEG")]
    public void Resize_JpegSource_OutputsWebP()
    {
        using var source = new MemoryStream(TestImages.Jpeg(1000, 800));

        var result = _sut.Resize(source)!;

        ImageSignature.Detect(result.Medium)!.ContentType.Should().Be("image/webp");
        ImageSignature.Detect(result.Thumbnail)!.ContentType.Should().Be("image/webp");
    }

    [Fact(DisplayName = "FR-JOB-002/D40: xoay theo EXIF Orientation trước khi resize, rồi bỏ metadata EXIF")]
    public void Resize_ExifRotated_AutoOrientsAndStripsExif()
    {
        // Ảnh lưu 1200×600 nhưng EXIF Orientation = 6 (xoay 90°) → hiển thị thật là 600×1200.
        using var source = new MemoryStream(TestImages.JpegWithExif(1200, 600, orientation: 6, make: "Camera test"));

        var result = _sut.Resize(source)!;

        TestImages.Size(result.Medium).Should().Be((300, 600));
        foreach (var output in new[] { result.Medium, result.Thumbnail })
        {
            var text = Encoding.ASCII.GetString(output);
            text.Should().NotContain("EXIF").And.NotContain("Exif").And.NotContain("Camera test");
        }
    }

    [Fact(DisplayName = "FR-JOB-002/D41: ảnh AVIF (Skia không decode được) → null, không ném lỗi")]
    public void Resize_Avif_ReturnsNull()
    {
        using var source = new MemoryStream(TestImages.AvifHeaderOnly());

        _sut.Resize(source).Should().BeNull();
    }

    [Fact(DisplayName = "FR-JOB-002/D40: ảnh vượt giới hạn điểm ảnh → null (chống decompression bomb), không decode")]
    public void Resize_OverPixelLimit_ReturnsNull()
    {
        var sut = new SkiaImageResizer(maxPixels: 100 * 100 - 1);
        using var source = new MemoryStream(TestImages.Png(100, 100));

        sut.Resize(source).Should().BeNull();
    }
}
