using Hangfire;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>FR-JOB-003 — đăng ký các Hangfire recurring job. Gọi một lần khi host khởi động (trừ Testing).</summary>
public static class RecurringJobsRegistration
{
    public const string SitemapJobId = "sitemap-generation";

    /// <summary>SRS FR-JOB-003: hàng ngày 02:00 UTC.</summary>
    public const string SitemapCron = "0 2 * * *";

    public static void RegisterRecurringJobs(this IServiceProvider services) => throw new NotImplementedException();

    public static void RegisterRecurringJobs(this IRecurringJobManager manager) => throw new NotImplementedException();
}
