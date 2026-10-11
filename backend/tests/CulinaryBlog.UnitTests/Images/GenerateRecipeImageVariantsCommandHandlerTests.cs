using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Commands.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-JOB-002 — D41 (AVIF/không decode được → no-op), D44 (idempotent, dọn file khi lỗi), D8 (tag cache).</summary>
public class GenerateRecipeImageVariantsCommandHandlerTests
{
    private const string OriginalUrl = "https://minio/culinary-blog/recipes/r/original.jpg";
    private const string MediumUrl = "https://minio/culinary-blog/recipes/r/medium.webp";
    private const string ThumbnailUrl = "https://minio/culinary-blog/recipes/r/thumb.webp";

    private static readonly byte[] MediumBytes = [1, 1, 1];
    private static readonly byte[] ThumbnailBytes = [2, 2, 2];

    private readonly IRecipeRepository _recipeRepository = Substitute.For<IRecipeRepository>();
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly IImageResizer _resizer = Substitute.For<IImageResizer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IBackgroundJobService _jobs = Substitute.For<IBackgroundJobService>();

    private readonly Recipe _recipe = Recipe.Create(
        "Phở bò", $"pho-bo-{Guid.NewGuid():N}", "mô tả", 10, 30, 2, RecipeDifficulty.Easy, Guid.NewGuid(), "owner-1");

    private readonly RecipeImage _image;

    public GenerateRecipeImageVariantsCommandHandlerTests()
    {
        _image = _recipe.AttachImage(OriginalUrl);

        _recipeRepository.GetByIdWithImagesAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
        _fileStorage.DownloadAsync(OriginalUrl, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([9, 9, 9])));
        _resizer.Resize(Arg.Any<Stream>()).Returns(new ResizedImageSet(MediumBytes, ThumbnailBytes));
        _fileStorage
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ReadAll(call.Arg<Stream>()).SequenceEqual(MediumBytes) ? MediumUrl : ThumbnailUrl);
    }

    private GenerateRecipeImageVariantsCommandHandler CreateHandler() =>
        new(_recipeRepository, _fileStorage, _resizer, _unitOfWork, _jobs,
            NullLogger<GenerateRecipeImageVariantsCommandHandler>.Instance);

    private GenerateRecipeImageVariantsCommand Command() => new(_recipe.Id, _image.Id);

    [Fact(DisplayName = "FR-JOB-002/D40/D41: thành công → upload 2 ảnh WebP vào recipes/{id}, gán URL, lưu DB")]
    public async Task Handle_Success_UploadsBothVariantsAndSaves()
    {
        await CreateHandler().Handle(Command(), CancellationToken.None);

        _image.MediumUrl.Should().Be(MediumUrl);
        _image.ThumbnailUrl.Should().Be(ThumbnailUrl);
        _image.OriginalUrl.Should().Be(OriginalUrl);
        await _fileStorage.Received(2).UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), IImageResizer.OutputContentType, $"recipes/{_recipe.Id}", Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _jobs.DidNotReceive().EnqueueDeleteImageFile(Arg.Any<string>());
    }

    [Fact(DisplayName = "FR-JOB-002/D8: thành công → xóa cache tag recipes + recipe:{slug}")]
    public async Task Handle_Success_SetsCacheTags()
    {
        var command = Command();

        await CreateHandler().Handle(command, CancellationToken.None);

        command.TagsToInvalidate.Should().BeEquivalentTo(["recipes", $"recipe:{_recipe.Slug}"]);
    }

    [Fact(DisplayName = "FR-JOB-002/D1/D44: recipe không còn (đã soft delete) → no-op, không tải ảnh, không xóa cache")]
    public async Task Handle_RecipeMissing_IsNoOp()
    {
        _recipeRepository.GetByIdWithImagesAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns((Recipe?)null);
        var command = Command();

        await CreateHandler().Handle(command, CancellationToken.None);

        await _fileStorage.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        command.TagsToInvalidate.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-JOB-002/D22/D44: ảnh không còn trong recipe (đã bị xóa) → no-op")]
    public async Task Handle_ImageMissing_IsNoOp()
    {
        var command = new GenerateRecipeImageVariantsCommand(_recipe.Id, Guid.NewGuid());

        await CreateHandler().Handle(command, CancellationToken.None);

        await _fileStorage.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact(DisplayName = "FR-JOB-002/D44: ảnh đã có ThumbnailUrl → no-op (job chạy lại an toàn)")]
    public async Task Handle_AlreadyHasVariants_IsNoOp()
    {
        _recipe.SetImageVariants(_image.Id, "https://minio/old-medium.webp", "https://minio/old-thumb.webp");

        await CreateHandler().Handle(Command(), CancellationToken.None);

        await _fileStorage.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default);
        _image.ThumbnailUrl.Should().Be("https://minio/old-thumb.webp");
    }

    [Fact(DisplayName = "FR-JOB-002/D41: resizer trả null (AVIF / vượt giới hạn điểm ảnh) → no-op, không upload, không ném lỗi")]
    public async Task Handle_ResizerReturnsNull_IsNoOp()
    {
        _resizer.Resize(Arg.Any<Stream>()).Returns((ResizedImageSet?)null);
        var command = Command();

        await CreateHandler().Handle(command, CancellationToken.None);

        await _fileStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default!, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        _image.ThumbnailUrl.Should().BeNull();
        command.TagsToInvalidate.Should().BeEmpty();
    }

    [Fact(DisplayName = "FR-JOB-002/D43/D44: lưu DB lỗi sau khi đã upload → enqueue xóa 2 file vừa upload rồi ném lại (để Hangfire retry)")]
    public async Task Handle_SaveFails_CleansUpUploadedVariantsAndRethrows()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _jobs.Received(1).EnqueueDeleteImageFile(MediumUrl);
        _jobs.Received(1).EnqueueDeleteImageFile(ThumbnailUrl);
        _jobs.DidNotReceive().EnqueueDeleteImageFile(OriginalUrl);
    }

    [Fact(DisplayName = "FR-JOB-002/D44: upload thumbnail lỗi sau khi medium đã lên → enqueue xóa medium rồi ném lại")]
    public async Task Handle_SecondUploadFails_CleansUpFirstAndRethrows()
    {
        _fileStorage
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(MediumUrl),
                _ => Task.FromException<string>(new HttpRequestException("minio down")));

        var act = () => CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
        _jobs.Received(1).EnqueueDeleteImageFile(MediumUrl);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    private static byte[] ReadAll(Stream stream)
    {
        stream.Position = 0;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        stream.Position = 0;
        return buffer.ToArray();
    }
}
