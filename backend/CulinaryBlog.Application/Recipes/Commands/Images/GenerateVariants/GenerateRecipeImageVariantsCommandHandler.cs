using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

#pragma warning disable CS9113 // TODO(FR-JOB-002): stub test-first — gỡ khi hiện thực
namespace CulinaryBlog.Application.Recipes.Commands.Images;

/// <summary>FR-JOB-002/D40–D44.</summary>
public sealed class GenerateRecipeImageVariantsCommandHandler(
    IRecipeRepository recipeRepository,
    IFileStorageService fileStorageService,
    IImageResizer imageResizer,
    IUnitOfWork unitOfWork,
    IBackgroundJobService backgroundJobService,
    ILogger<GenerateRecipeImageVariantsCommandHandler> logger)
    : IRequestHandler<GenerateRecipeImageVariantsCommand>
{
    public Task Handle(GenerateRecipeImageVariantsCommand request, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
