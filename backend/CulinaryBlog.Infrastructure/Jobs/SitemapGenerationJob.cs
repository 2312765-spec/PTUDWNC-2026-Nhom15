using CulinaryBlog.Application.Seo.Commands.GenerateSitemap;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>
/// FR-JOB-003 — Hangfire recurring job, chỉ chuyển tiếp sang MediatR command (D44).
/// SRS: retry 2 lần. D53: DisableConcurrentExecution (lock trên Postgres storage) thay RedLock.
/// </summary>
[AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
[DisableConcurrentExecution(timeoutInSeconds: 600)]
public sealed class SitemapGenerationJob(ISender sender, IConfiguration configuration)
{
    /// <summary>Hangfire thay <paramref name="ct"/> bằng token huỷ của server khi chạy job.</summary>
    public Task ExecuteAsync(CancellationToken ct) =>
        sender.Send(new GenerateSitemapCommand(configuration["Seo:SiteBaseUrl"] ?? string.Empty), ct);
}
