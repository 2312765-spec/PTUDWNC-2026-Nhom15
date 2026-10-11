using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Application.Recipes.Commands.Images;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Enums;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CulinaryBlog.UnitTests.Images;

/// <summary>FR-RCP-008/FR-FILE-002/D43 — xóa ảnh enqueue xóa mọi file của ảnh đó trên MinIO.</summary>
public class DeleteRecipeImageCommandHandlerTests
{
    private const string OwnerId = "owner-1";

    private readonly IRecipeRepository _recipeRepository = Substitute.For<IRecipeRepository>();
    private readonly IRepository<RecipeImage> _imageRepository = Substitute.For<IRepository<RecipeImage>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IBackgroundJobService _jobs = Substitute.For<IBackgroundJobService>();

    private readonly Recipe _recipe = Recipe.Create(
        "Phở bò", $"pho-bo-{Guid.NewGuid():N}", "mô tả", 10, 30, 2, RecipeDifficulty.Easy, Guid.NewGuid(), OwnerId);

    public DeleteRecipeImageCommandHandlerTests()
    {
        _currentUser.UserId.Returns(OwnerId);
        _recipeRepository.GetByIdWithImagesAsync(_recipe.Id, Arg.Any<CancellationToken>()).Returns(_recipe);
    }

    private DeleteRecipeImageCommandHandler CreateHandler() =>
        new(_recipeRepository, _imageRepository, _unitOfWork, _currentUser, _jobs);

    [Fact(DisplayName = "D43: ảnh đã có medium + thumbnail → enqueue xóa cả 3 file")]
    public async Task Handle_ImageWithVariants_EnqueuesDeleteForAllThree()
    {
        var image = _recipe.AttachImage("https://minio/original.jpg");
        _recipe.SetImageVariants(image.Id, "https://minio/medium.webp", "https://minio/thumb.webp");

        await CreateHandler().Handle(new DeleteRecipeImageCommand(_recipe.Id, image.Id), CancellationToken.None);

        _jobs.Received(1).EnqueueDeleteImageFile("https://minio/original.jpg");
        _jobs.Received(1).EnqueueDeleteImageFile("https://minio/medium.webp");
        _jobs.Received(1).EnqueueDeleteImageFile("https://minio/thumb.webp");
    }

    [Fact(DisplayName = "D43: ảnh chưa có medium/thumbnail (job chưa chạy) → chỉ enqueue xóa ảnh gốc")]
    public async Task Handle_ImageWithoutVariants_EnqueuesOnlyOriginal()
    {
        var image = _recipe.AttachImage("https://minio/original.jpg");

        await CreateHandler().Handle(new DeleteRecipeImageCommand(_recipe.Id, image.Id), CancellationToken.None);

        _jobs.Received(1).EnqueueDeleteImageFile("https://minio/original.jpg");
        _jobs.ReceivedCalls().Should().ContainSingle();
    }
}
