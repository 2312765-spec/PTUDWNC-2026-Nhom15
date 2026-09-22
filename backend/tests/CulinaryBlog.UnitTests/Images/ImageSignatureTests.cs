using System.Text;
using CulinaryBlog.Application.Common.Files;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-RCP-008 / FR-FILE-001 / D24 / CONS-007 — nhận diện định dạng bằng magic bytes.</summary>
public class ImageSignatureTests
{
    [Fact(DisplayName = "FR-FILE-001/D24: nhận ra JPEG (FF D8 FF)")]
    public void Detect_Jpeg()
    {
        var result = ImageSignature.Detect(ImageTestData.Jpeg());

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
    }

    [Fact(DisplayName = "FR-FILE-001/D24: nhận ra PNG (89 50 4E 47 0D 0A 1A 0A)")]
    public void Detect_Png()
    {
        var result = ImageSignature.Detect(ImageTestData.Png());

        result!.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact(DisplayName = "FR-FILE-001/D24: nhận ra WebP (RIFF....WEBP)")]
    public void Detect_WebP()
    {
        var result = ImageSignature.Detect(ImageTestData.WebP());

        result!.ContentType.Should().Be("image/webp");
        result.Extension.Should().Be(".webp");
    }

    [Theory(DisplayName = "FR-FILE-001/D24: nhận ra AVIF với brand avif và avis")]
    [InlineData("avif")]
    [InlineData("avis")]
    public void Detect_Avif(string brand)
    {
        var header = ImageTestData.Avif();
        Encoding.ASCII.GetBytes(brand).CopyTo(header, 8);

        var result = ImageSignature.Detect(header);

        result!.ContentType.Should().Be("image/avif");
        result.Extension.Should().Be(".avif");
    }

    [Fact(DisplayName = "FR-FILE-001/D24: file .exe (MZ) không phải ảnh")]
    public void Detect_Exe_ReturnsNull() =>
        ImageSignature.Detect(ImageTestData.Exe()).Should().BeNull();

    [Fact(DisplayName = "FR-FILE-001/D24: WAV có 4 byte đầu RIFF nhưng không phải WebP")]
    public void Detect_Wav_ReturnsNull() =>
        ImageSignature.Detect(ImageTestData.Wav()).Should().BeNull();

    [Fact(DisplayName = "FR-FILE-001/D24: ftyp với brand khác (mp4) không phải AVIF")]
    public void Detect_Mp4_ReturnsNull()
    {
        var header = ImageTestData.Avif();
        Encoding.ASCII.GetBytes("isom").CopyTo(header, 8);

        ImageSignature.Detect(header).Should().BeNull();
    }

    [Theory(DisplayName = "FR-FILE-001/D24: header quá ngắn hoặc rỗng → null, không ném lỗi")]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(11)]
    public void Detect_TooShort_ReturnsNull(int length) =>
        ImageSignature.Detect(new byte[length]).Should().BeNull();
}
