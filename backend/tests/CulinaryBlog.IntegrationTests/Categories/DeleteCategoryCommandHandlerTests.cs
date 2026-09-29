using CulinaryBlog.Application.Categories.Commands.DeleteCategory;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.UnitTests.Categories;

public sealed class DeleteCategoryCommandHandlerTests
{
    [Fact(DisplayName = "FR-CAT-005: Xóa danh mục không tồn tại → ném NotFoundException")]
    public async Task Handle_CategoryNotFound_ThrowsNotFoundException()
    {
        var repo = new TestCategoryRepository { Category = null };
        var uow = new TestUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(repo, uow);

        var act = async () => await handler.Handle(new DeleteCategoryCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "FR-CAT-005: Xóa danh mục còn công thức → ném ConflictException (409)")]
    public async Task Handle_CategoryHasRecipes_ThrowsConflictException()
    {
        var cat = Category.Create("Món bánh", "mon-banh", "Mô tả");
        var repo = new TestCategoryRepository { Category = cat, RecipeCount = 5 };
        var uow = new TestUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(repo, uow);

        var act = async () => await handler.Handle(new DeleteCategoryCommand(cat.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact(DisplayName = "FR-CAT-005: Xóa danh mục không còn công thức → Soft delete thành công")]
    public async Task Handle_EmptyCategory_SoftDeletesSuccessfully()
    {
        var cat = Category.Create("Món xào", "mon-xao", "Mô tả");
        var repo = new TestCategoryRepository { Category = cat, RecipeCount = 0 };
        var uow = new TestUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(repo, uow);

        await handler.Handle(new DeleteCategoryCommand(cat.Id), CancellationToken.None);

        cat.IsDeleted.Should().BeTrue();
        repo.Updated.Should().BeTrue();
        uow.Saved.Should().BeTrue();
    }

    private sealed class TestCategoryRepository : ICategoryRepository
    {
        public Category? Category { get; set; }
        public int RecipeCount { get; set; }
        public bool Updated { get; private set; }

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Category);
        public Task<int> GetTotalRecipeCountAsync(Guid categoryId, CancellationToken ct = default) => Task.FromResult(RecipeCount);
        public void Update(Category category) => Updated = true;

        public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Category?>(null);
        public Task<Category?> GetBySlugWithRecipesAsync(string slug, CancellationToken ct = default) => Task.FromResult<Category?>(null);
        public Task<IReadOnlyList<Category>> GetAllWithRecipesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Category>>(Array.Empty<Category>());
        public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsByNameAsync(string name, Guid excludeId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ExistsBySlugAsync(string slug, Guid excludeId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GetPublishedRecipeCountAsync(Guid categoryId, CancellationToken ct = default) => Task.FromResult(RecipeCount);
        public Task AddAsync(Category category, CancellationToken ct = default) => Task.CompletedTask;
        public void Delete(Category category) => category.Delete();
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public bool Saved { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            Saved = true;
            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) => action(ct);
        public Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default) => action();
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct);
        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default) => action();
    }
}