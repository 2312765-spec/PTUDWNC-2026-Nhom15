using CulinaryBlog.Application.Common.Behaviors;
using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.UnitTests.Observability;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CulinaryBlog.UnitTests.Common.Behaviors;

/// <summary>D8 — Command ICacheInvalidator xóa tag SAU khi handler thành công; lỗi xóa cache không làm hỏng lệnh ghi.</summary>
public sealed class CacheInvalidationBehaviorTests
{
    private sealed record WriteCommand(IReadOnlyList<string> TagsToInvalidate) : IRequest<string>, ICacheInvalidator;

    private sealed record PlainCommand : IRequest<string>;

    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    [Fact(DisplayName = "D8: handler thành công → xóa đúng các tag, và chỉ xóa SAU khi handler chạy xong")]
    public async Task Success_RemovesTagsAfterHandler()
    {
        var order = new List<string>();
        _cache.RemoveByTagsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => { order.Add("xóa cache"); return Task.CompletedTask; });
        var sut = Create<WriteCommand>();

        var result = await sut.Handle(new WriteCommand(["categories", "recipes"]), () =>
        {
            order.Add("handler");
            return Task.FromResult("ok");
        }, CancellationToken.None);

        result.Should().Be("ok");
        order.Should().Equal("handler", "xóa cache");
        await _cache.Received(1).RemoveByTagsAsync(
            Arg.Is<IEnumerable<string>>(tags => tags.SequenceEqual(new[] { "categories", "recipes" })),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "D8: handler ném lỗi → KHÔNG xóa cache (dữ liệu chưa đổi), lỗi đi lên nguyên vẹn")]
    public async Task HandlerThrows_DoesNotInvalidate()
    {
        var sut = Create<WriteCommand>();

        var act = () => sut.Handle(new WriteCommand(["recipes"]), () => throw new InvalidOperationException("ghi lỗi"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "D8: command không implement ICacheInvalidator → không đụng cache")]
    public async Task NotInvalidator_DoesNotTouchCache()
    {
        var sut = Create<PlainCommand>();

        await sut.Handle(new PlainCommand(), () => Task.FromResult("ok"), CancellationToken.None);

        _cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "D8: danh sách tag rỗng → không gọi Redis")]
    public async Task EmptyTags_DoesNotTouchCache()
    {
        var sut = Create<WriteCommand>();

        await sut.Handle(new WriteCommand([]), () => Task.FromResult("ok"), CancellationToken.None);

        _cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "NFR-REL-002: xóa cache ném lỗi → log Warning, lệnh ghi vẫn trả thành công")]
    public async Task RemoveThrows_StillReturnsResponse()
    {
        _cache.RemoveByTagsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("redis down"));
        var logger = new FakeLogger<CacheInvalidationBehavior<WriteCommand, string>>();
        var sut = new CacheInvalidationBehavior<WriteCommand, string>(_cache, logger);

        var result = await sut.Handle(new WriteCommand(["recipes"]), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    private CacheInvalidationBehavior<TRequest, string> Create<TRequest>() where TRequest : notnull =>
        new(_cache, new FakeLogger<CacheInvalidationBehavior<TRequest, string>>());
}
