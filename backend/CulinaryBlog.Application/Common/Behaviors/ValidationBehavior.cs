using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline #2 (SRS 6.3) — CONS-008: validation CHỈ ở đây, KHÔNG ở endpoint handler.
/// ValidationException được GlobalExceptionMiddleware map sang HTTP 400 (D4 — SRS Chương 3 ghi 422, sai).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
