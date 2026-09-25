using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Commands.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-RCP-008 — thứ tự kiểm tra quyền → upload MinIO → lưu DB, và dọn file khi lưu DB lỗi.</summary>
public class UploadRecipeImageCommandHandlerTests
{
    private const string OwnerId = "owner-1";
    private const string UploadedUrl = "https://fake-minio.local/culinary-blog/recipes/x/a.jpg";

    private readonly IRecipeRepository _recipeRepository = Substitute.For<IRecipeRepository>();
    private readonly IRepository<RecipeImage> _imageRepository = Substitute.For<IRepository<RecipeImage>>();
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IBackgroundJobService _jobs = Substitute.For<IBackgroundJobService>();

    public UploadRecipeImageCommandHandlerTests()
    {
        _currentUser.UserId.Returns(OwnerId);
        _fileStorage
            .UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UploadedUrl);
        // Chạy khối transaction thật (giả lập không retry) để kiểm tra logic bên trong.
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
    }

    private UploadRecipeImageCommandHandler CreateHandler() =>
        new(_recipeRepository, _imageRepository, _fileStorage, _unitOfWork, _currentUser, _jobs);

    private static UploadRecipeImageCommand Command(Guid recipeId) =>
        new(recipeId, new MemoryStream([1, 2, 3]), 3, "image/jpeg", "a.jpg", null);

    private static Recipe NewRecipe() =>
        Recipe.Create("Phở bò", $"pho-{Guid.NewGuid():N}", "mô tả", 10, 30, 2, RecipeDifficulty.Easy, Guid.NewGuid(), OwnerId);

    [Fact(DisplayName = "FR-RCP-008: recipe không tồn tại → 404 và KHÔNG upload file nào lên MinIO")]
    public async Task Handle_RecipeNotFound_DoesNotUpload()
    {
        _recipeRepository.GetAuthorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        var act = () => CreateHandler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.RecipeNotFound);
        await _fileStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "FR-RCP-008: không phải chủ sở hữu → 403 và KHÔNG upload file nào lên MinIO")]
    public async Task Handle_NotOwner_DoesNotUpload()
    {
        _recipeRepository.GetAuthorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns("someone-else");
        _currentUser.IsAdmin.Returns(false);

        var act = () => CreateHandler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ForbiddenException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.RecipeForbidden);
        await _fileStorage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default!, default);
    }

    [Fact(DisplayName = "FR-RCP-008: lưu DB lỗi sau khi đã upload → enqueue xoá file MinIO (không để file mồ côi) rồi ném lại lỗi")]
    public async Task Handle_DbFailsAfterUpload_EnqueuesFileCleanup()
    {
        var recipe = NewRecipe();
        _recipeRepository.GetAuthorIdAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(OwnerId);
        _recipeRepository.GetByIdWithImagesForUpdateAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns<int>(_ => throw new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(Command(recipe.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _jobs.Received(1).EnqueueDeleteImageFile(UploadedUrl);
    }

    [Fact(DisplayName = "FR-RCP-008: thành công → ảnh đầu tiên primary, không enqueue xoá file")]
    public async Task Handle_Success_ReturnsPrimaryAndDoesNotCleanup()
    {
        var recipe = NewRecipe();
        _recipeRepository.GetAuthorIdAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(OwnerId);
        _recipeRepository.GetByIdWithImagesForUpdateAsync(recipe.Id, Arg.Any<CancellationToken>()).Returns(recipe);

        var result = await CreateHandler().Handle(Command(recipe.Id), CancellationToken.None);

        result.IsPrimary.Should().BeTrue();
        result.OriginalUrl.Should().Be(UploadedUrl);
        _jobs.DidNotReceive().EnqueueDeleteImageFile(Arg.Any<string>());
    }
}
