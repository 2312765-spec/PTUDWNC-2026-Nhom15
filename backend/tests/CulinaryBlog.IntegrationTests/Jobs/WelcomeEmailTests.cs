using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Auth.Dtos;
using CulinaryBlog.Application.Common.Email;
using CulinaryBlog.Infrastructure.Jobs;
using CulinaryBlog.IntegrationTests.Common;
using CulinaryBlog.IntegrationTests.Recipes.Support;
using FluentAssertions;
using FluentValidation;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Jobs;

/// <summary>
/// FR-JOB-001 — docs/decisions.md D12, D42, D55–D57. Postgres thật (Testcontainers),
/// <see cref="RecordingBackgroundJobService"/> thay Hangfire (Testing không chạy server, D45) và
/// <see cref="RecordingEmailService"/> thay SMTP. Mỗi test dùng email riêng nên không đếm tổng.
/// </summary>
public sealed class WelcomeEmailTests(RecipeImagesApiFactory factory) : IClassFixture<RecipeImagesApiFactory>
{
    private const string BaseUrl = RecipeImagesApiFactory.SiteBaseUrl;
    private const string Password = "Str0ng!Pass1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client = factory.CreateClient();

    // ---------------- Enqueue (trigger) ----------------

    [Fact(DisplayName = "FR-JOB-001: đăng ký thành công → enqueue welcome email đúng email + displayName")]
    public async Task Register_EnqueuesWelcomeEmail()
    {
        var email = NewEmail();

        var response = await RegisterAsync(email, "Nguyễn Văn A");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        factory.BackgroundJobs.WelcomeEmails.Should().ContainSingle(w => w.Email == email)
            .Which.DisplayName.Should().Be("Nguyễn Văn A");
    }

    [Fact(DisplayName = "FR-JOB-001/D56: đăng nhập Google tạo tài khoản mới → enqueue welcome email")]
    public async Task GoogleLogin_NewUser_EnqueuesWelcomeEmail()
    {
        var email = NewEmail();

        var response = await GoogleLoginAsync(email, "Người Google Mới");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.BackgroundJobs.WelcomeEmails.Should().ContainSingle(w => w.Email == email)
            .Which.DisplayName.Should().Be("Người Google Mới");
    }

    [Fact(DisplayName = "FR-JOB-001/D56: Google liên kết vào tài khoản đã đăng ký → không gửi thêm welcome email")]
    public async Task GoogleLogin_LinkExistingUser_DoesNotEnqueueAgain()
    {
        var email = NewEmail();
        (await RegisterAsync(email, "Đã Đăng Ký")).EnsureSuccessStatusCode();

        var response = await GoogleLoginAsync(email, "Tên Trên Google");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.BackgroundJobs.WelcomeEmails.Count(w => w.Email == email).Should().Be(1);
    }

    [Fact(DisplayName = "FR-JOB-001: đăng ký lỗi (email trùng → 409) → không enqueue welcome email")]
    public async Task Register_DuplicateEmail_DoesNotEnqueue()
    {
        var email = NewEmail();
        (await RegisterAsync(email, "Lần Đầu")).EnsureSuccessStatusCode();

        var response = await RegisterAsync(email, "Lần Hai");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        factory.BackgroundJobs.WelcomeEmails.Count(w => w.Email == email).Should().Be(1);
    }

    [Fact(DisplayName = "FR-JOB-001/D55: enqueue welcome email lỗi → đăng ký vẫn 201, đủ access + refresh token")]
    public async Task Register_EnqueueFails_StillReturns201WithTokens()
    {
        factory.BackgroundJobs.ThrowOnWelcomeEmail = true;
        try
        {
            var response = await RegisterAsync(NewEmail(), "Hangfire Hỏng");

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
            body!.AccessToken.Should().NotBeNullOrWhiteSpace();
            body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            factory.BackgroundJobs.ThrowOnWelcomeEmail = false;
        }
    }

    [Fact(DisplayName = "FR-JOB-001/D55: enqueue welcome email lỗi → Google login tài khoản mới vẫn 200")]
    public async Task GoogleLogin_EnqueueFails_StillReturns200()
    {
        factory.BackgroundJobs.ThrowOnWelcomeEmail = true;
        try
        {
            var response = await GoogleLoginAsync(NewEmail(), "Google Hangfire Hỏng");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            factory.BackgroundJobs.ThrowOnWelcomeEmail = false;
        }
    }

    [Fact(DisplayName = "FR-JOB-001/D57: HangfireBackgroundJobService enqueue WelcomeEmailJob.ExecuteAsync (không gọi thẳng IEmailService)")]
    public void HangfireBackgroundJobService_EnqueuesWelcomeEmailJob()
    {
        var client = new RecordingBackgroundJobClient();
        var service = new HangfireBackgroundJobService(client);

        service.EnqueueWelcomeEmail("a@example.com", "Tên A");

        var (job, state) = client.Created.Should().ContainSingle().Subject;
        state.Should().BeOfType<EnqueuedState>();
        job.Type.Should().Be<WelcomeEmailJob>();
        job.Method.Name.Should().Be(nameof(WelcomeEmailJob.ExecuteAsync));
        job.Args.Should().StartWith(["a@example.com", "Tên A"]);
    }

    // ---------------- Job ----------------

    [Fact(DisplayName = "FR-JOB-001/D42: WelcomeEmailJob retry 3 lần, chờ 1/5/30 phút, hết lượt → Failed")]
    public void Job_HasRetryPolicy_3Attempts_1_5_30Minutes()
    {
        var method = typeof(WelcomeEmailJob).GetMethod(nameof(WelcomeEmailJob.ExecuteAsync))!;
        var retry = typeof(WelcomeEmailJob).GetCustomAttributes(inherit: true)
            .Concat(method.GetCustomAttributes(inherit: true))
            .OfType<AutomaticRetryAttribute>().Single();

        retry.Attempts.Should().Be(3);
        retry.DelaysInSeconds.Should().Equal(60, 300, 1800);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }

    [Fact(DisplayName = "FR-JOB-001/D12/D57: job gửi email HTML + text tới đúng người, có tên và link / + /dashboard, không có link kích hoạt")]
    public async Task Job_SendsWelcomeEmail_WithNameAndLinks()
    {
        var email = NewEmail();

        await RunJobAsync(email, "Trần Thị B");

        var message = SentTo(email);
        message.ToName.Should().Be("Trần Thị B");
        message.Subject.Should().NotBeNullOrWhiteSpace();

        message.HtmlBody.Should().Contain("Trần Thị B");
        message.HtmlBody.Should().Contain($"href=\"{BaseUrl}/\"");
        message.HtmlBody.Should().Contain($"href=\"{BaseUrl}/dashboard\"");

        message.TextBody.Should().Contain("Trần Thị B");
        message.TextBody.Should().Contain($"{BaseUrl}/dashboard");

        // D12: không có luồng xác nhận email.
        foreach (var body in new[] { message.HtmlBody, message.TextBody })
        {
            body.Should().NotContainEquivalentOf("kích hoạt");
            body.Should().NotContainEquivalentOf("verify");
            body.Should().NotContainEquivalentOf("confirm");
        }
    }

    [Fact(DisplayName = "FR-JOB-001/D57: displayName chứa HTML bị encode trong phần HTML của email")]
    public async Task Job_EncodesDisplayNameInHtml()
    {
        var email = NewEmail();

        await RunJobAsync(email, "<script>alert(1)</script>");

        var message = SentTo(email);
        message.HtmlBody.Should().NotContain("<script>");
        message.HtmlBody.Should().Contain("&lt;script&gt;");
    }

    [Fact(DisplayName = "FR-JOB-001/D57: thiếu Seo:SiteBaseUrl → job lỗi (ValidationException), không gửi email")]
    public async Task Job_MissingSiteBaseUrl_ThrowsAndSendsNothing()
    {
        var email = NewEmail();
        using var scope = factory.Services.CreateScope();
        var job = new WelcomeEmailJob(
            scope.ServiceProvider.GetRequiredService<ISender>(),
            new ConfigurationBuilder().Build());

        var act = () => job.ExecuteAsync(email, "Không Có Base Url", CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        factory.Emails.Sent.Should().NotContain(m => m.ToAddress == email);
    }

    [Theory(DisplayName = "FR-JOB-001/D57: command sai (email sai, displayName rỗng, base URL không tuyệt đối http(s)) → ValidationException")]
    [InlineData("khong-phai-email", "Tên", BaseUrl)]
    [InlineData("ok@example.com", "  ", BaseUrl)]
    [InlineData("ok@example.com", "Tên", "culinary.test")]
    [InlineData("ok@example.com", "Tên", "ftp://culinary.test")]
    public async Task Command_Invalid_ThrowsValidation(string email, string displayName, string siteBaseUrl)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new Application.Auth.Commands.SendWelcomeEmail.SendWelcomeEmailCommand(
            email, displayName, siteBaseUrl));

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ---------------- Helpers ----------------

    private static string NewEmail() => $"welcome-{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> RegisterAsync(string email, string displayName) =>
        _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, displayName });

    private Task<HttpResponseMessage> GoogleLoginAsync(string email, string name) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.ForUser(email, name, null, Guid.NewGuid().ToString()) });

    /// <summary>Chạy đúng đường Hangfire sẽ chạy: resolve job từ DI → ExecuteAsync → MediatR command.</summary>
    private async Task RunJobAsync(string email, string displayName)
    {
        using var scope = factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<WelcomeEmailJob>();
        await job.ExecuteAsync(email, displayName, CancellationToken.None);
    }

    private EmailMessage SentTo(string email) =>
        factory.Emails.Sent.Should().ContainSingle(m => m.ToAddress == email).Subject;

    /// <summary>Thay IBackgroundJobClient của Hangfire — ghi lại job được tạo, không cần JobStorage.</summary>
    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public List<(Job Job, IState State)> Created { get; } = [];

        public string Create(Job job, IState state)
        {
            Created.Add((job, state));
            return Created.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => throw new NotSupportedException();
    }
}
