using CulinaryBlog.Application.Common.Behaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.UnitTests.Observability;

public sealed class LoggingBehaviorTests
{
    private sealed record Ping : IRequest<string>;

    [Fact(DisplayName = "FR-OBS-002: request nhanh -> log Information bắt đầu + xong, không Warning")]
    public async Task Handle_FastRequest_LogsInformationOnly()
    {
        var logger = new FakeLogger<LoggingBehavior<Ping, string>>();
        var sut = new LoggingBehavior<Ping, string>(logger);

        var result = await sut.Handle(new Ping(), () => Task.FromResult("pong"), CancellationToken.None);

        result.Should().Be("pong");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("Bắt đầu"));
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("xong"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning);
    }

    [Fact(DisplayName = "FR-OBS-002: request > 500ms -> log Warning CHẬM")]
    public async Task Handle_SlowRequest_LogsWarning()
    {
        var logger = new FakeLogger<LoggingBehavior<Ping, string>>();
        var sut = new LoggingBehavior<Ping, string>(logger);

        await sut.Handle(new Ping(), async () =>
        {
            await Task.Delay(600);
            return "pong";
        }, CancellationToken.None);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("CHẬM"));
    }

    [Fact(DisplayName = "FR-OBS-002: handler ném lỗi -> log Error kèm exception, rồi rethrow nguyên vẹn")]
    public async Task Handle_HandlerThrows_LogsErrorAndRethrows()
    {
        var logger = new FakeLogger<LoggingBehavior<Ping, string>>();
        var sut = new LoggingBehavior<Ping, string>(logger);
        var boom = new InvalidOperationException("boom");

        var act = () => sut.Handle(new Ping(), () => throw boom, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Exception == boom);
    }
}
