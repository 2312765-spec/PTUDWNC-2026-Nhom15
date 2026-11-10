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

        // Mỗi validator một ValidationContext riêng và chạy TUẦN TỰ (NFR-MAINT-002, 2026-11-10):
        // - Dùng chung một context thì FluentValidation cộng dồn lỗi vào đó → validator sau trả cả lỗi
        //   của validator trước, mỗi lỗi bị báo 2 lần khi có ≥ 2 validator.
        // - Task.WhenAll chạy song song: validator có rule async đọc DB sẽ dùng chung DbContext scoped
        //   cùng lúc → EF Core ném "A second operation was started on this context".
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors.Where(f => f is not null));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
