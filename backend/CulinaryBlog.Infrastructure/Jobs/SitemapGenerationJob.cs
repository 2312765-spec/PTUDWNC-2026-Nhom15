using MediatR;
using Microsoft.Extensions.Configuration;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>FR-JOB-003 — Hangfire recurring job, chỉ chuyển tiếp sang MediatR command (D44).</summary>
public sealed class SitemapGenerationJob(ISender sender, IConfiguration configuration)
{
    public Task ExecuteAsync(CancellationToken ct)
    {
        _ = (sender, configuration);
        throw new NotImplementedException();
    }
}
