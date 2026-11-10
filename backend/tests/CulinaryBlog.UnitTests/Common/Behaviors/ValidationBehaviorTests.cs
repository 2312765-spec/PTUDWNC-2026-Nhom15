using CulinaryBlog.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace CulinaryBlog.UnitTests.Common.Behaviors;

/// <summary>CONS-008 / D4 — validation chỉ chạy trong pipeline; lỗi → ValidationException (middleware map 400).</summary>
public sealed class ValidationBehaviorTests
{
    private sealed record Ping(string Name, int Age) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<Ping>
    {
        public NameValidator() => RuleFor(p => p.Name).NotEmpty();
    }

    private sealed class AgeValidator : AbstractValidator<Ping>
    {
        public AgeValidator() => RuleFor(p => p.Age).GreaterThan(0);
    }

    [Fact(DisplayName = "CONS-008: request không có validator → gọi thẳng handler")]
    public async Task NoValidators_CallsHandler()
    {
        var sut = new ValidationBehavior<Ping, string>([]);

        var result = await sut.Handle(new Ping("", -1), () => Task.FromResult("pong"), CancellationToken.None);

        result.Should().Be("pong");
    }

    [Fact(DisplayName = "CONS-008: request hợp lệ → gọi handler")]
    public async Task Valid_CallsHandler()
    {
        var sut = new ValidationBehavior<Ping, string>([new NameValidator(), new AgeValidator()]);

        var result = await sut.Handle(new Ping("Thuận", 20), () => Task.FromResult("pong"), CancellationToken.None);

        result.Should().Be("pong");
    }

    [Fact(DisplayName = "CONS-008/D4: request sai → ValidationException, KHÔNG gọi handler")]
    public async Task Invalid_ThrowsAndSkipsHandler()
    {
        var sut = new ValidationBehavior<Ping, string>([new NameValidator()]);
        var handlerCalled = false;

        var act = () => sut.Handle(new Ping("", 20), () =>
        {
            handlerCalled = true;
            return Task.FromResult("pong");
        }, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(Ping.Name));
        handlerCalled.Should().BeFalse("handler không được chạy với dữ liệu sai");
    }

    [Fact(DisplayName = "CONS-008: nhiều validator cùng lỗi → gộp đủ lỗi của tất cả vào một ValidationException")]
    public async Task MultipleValidators_AggregatesAllFailures()
    {
        var sut = new ValidationBehavior<Ping, string>([new NameValidator(), new AgeValidator()]);

        var act = () => sut.Handle(new Ping("", 0), () => Task.FromResult("pong"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo([nameof(Ping.Name), nameof(Ping.Age)]);
    }
}
