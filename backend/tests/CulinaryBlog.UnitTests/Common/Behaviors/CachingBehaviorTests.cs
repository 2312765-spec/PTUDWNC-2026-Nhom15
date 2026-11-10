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

/// <summary>D8 — cache chỉ qua CachingBehavior; NFR-REL-002/D90 — lỗi cache không bao giờ làm hỏng request.</summary>
public sealed class CachingBehaviorTests
{
    private sealed record CachedPing : IRequest<string>, ICacheable
    {
        public string CacheKey => "ping:1";
        public TimeSpan CacheTtl => TimeSpan.FromMinutes(5);
        public IReadOnlyList<string> CacheTags => ["pings", "ping:1"];
    }

    private sealed record PlainPing : IRequest<string>;

    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    [Fact(DisplayName = "D8: request không implement ICacheable → không đụng cache, gọi handler")]
    public async Task NotCacheable_BypassesCache()
    {
        var sut = new CachingBehavior<PlainPing, string>(_cache, new FakeLogger<CachingBehavior<PlainPing, string>>());

        var result = await sut.Handle(new PlainPing(), () => Task.FromResult("pong"), CancellationToken.None);

        result.Should().Be("pong");
        _cache.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "D8: cache HIT → trả bản trong cache, KHÔNG gọi handler")]
    public async Task Hit_ReturnsCachedWithoutCallingHandler()
    {
        _cache.GetAsync<string>("ping:1", Arg.Any<CancellationToken>()).Returns("từ cache");
        var sut = CreateCached();
        var handlerCalled = false;

        var result = await sut.Handle(new CachedPing(), () =>
        {
            handlerCalled = true;
            return Task.FromResult("từ DB");
        }, CancellationToken.None);

        result.Should().Be("từ cache");
        handlerCalled.Should().BeFalse();
        await _cache.DidNotReceiveWithAnyArgs().SetAsync<string>(default!, default!, default, default!, default);
    }

    [Fact(DisplayName = "D8: cache MISS → gọi handler rồi ghi cache đúng key, TTL, tag")]
    public async Task Miss_CallsHandlerAndStoresWithKeyTtlTags()
    {
        _cache.GetAsync<string>("ping:1", Arg.Any<CancellationToken>()).Returns((string?)null);
        var sut = CreateCached();

        var result = await sut.Handle(new CachedPing(), () => Task.FromResult("từ DB"), CancellationToken.None);

        result.Should().Be("từ DB");
        await _cache.Received(1).SetAsync(
            "ping:1",
            "từ DB",
            TimeSpan.FromMinutes(5),
            Arg.Is<IEnumerable<string>>(tags => tags.SequenceEqual(new[] { "pings", "ping:1" })),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "NFR-REL-002: đọc cache ném lỗi → log Warning, vẫn gọi handler, không throw")]
    public async Task GetThrows_FallsBackToHandler()
    {
        _cache.GetAsync<string>("ping:1", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("redis down"));
        var logger = new FakeLogger<CachingBehavior<CachedPing, string>>();
        var sut = new CachingBehavior<CachedPing, string>(_cache, logger);

        var result = await sut.Handle(new CachedPing(), () => Task.FromResult("từ DB"), CancellationToken.None);

        result.Should().Be("từ DB");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception is InvalidOperationException);
    }

    [Fact(DisplayName = "NFR-REL-002: ghi cache ném lỗi → log Warning, vẫn trả kết quả của handler")]
    public async Task SetThrows_StillReturnsResponse()
    {
        _cache.GetAsync<string>("ping:1", Arg.Any<CancellationToken>()).Returns((string?)null);
        _cache.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("redis down"));
        var logger = new FakeLogger<CachingBehavior<CachedPing, string>>();
        var sut = new CachingBehavior<CachedPing, string>(_cache, logger);

        var result = await sut.Handle(new CachedPing(), () => Task.FromResult("từ DB"), CancellationToken.None);

        result.Should().Be("từ DB");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning);
    }

    [Fact(DisplayName = "D8: handler ném lỗi → lỗi đi lên nguyên vẹn, không ghi cache")]
    public async Task HandlerThrows_PropagatesAndDoesNotStore()
    {
        _cache.GetAsync<string>("ping:1", Arg.Any<CancellationToken>()).Returns((string?)null);
        var sut = CreateCached();

        var act = () => sut.Handle(new CachedPing(), () => throw new KeyNotFoundException("không có"), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        await _cache.DidNotReceiveWithAnyArgs().SetAsync<string>(default!, default!, default, default!, default);
    }

    private CachingBehavior<CachedPing, string> CreateCached() =>
        new(_cache, new FakeLogger<CachingBehavior<CachedPing, string>>());
}
