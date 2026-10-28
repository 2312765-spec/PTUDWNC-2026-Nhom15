using System.Net.Http.Json;
using System.Text.Json;
using CulinaryBlog.Application.Common.Email;
using CulinaryBlog.Infrastructure.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Xunit;

namespace CulinaryBlog.IntegrationTests.Email;

/// <summary>
/// FR-JOB-001/D57 — <see cref="MailKitEmailService"/> gửi qua SMTP thật tới Mailhog (Testcontainers),
/// giống môi trường dev (docker-compose). Đọc lại thư thô qua HTTP API của Mailhog rồi parse bằng MimeKit
/// để không phụ thuộc cách Mailhog hiển thị header tiếng Việt.
/// </summary>
public sealed class MailKitEmailServiceTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;
    private const string FromAddress = "no-reply@culinary.test";
    private const string FromName = "Culinary Blog";

    private readonly IContainer _mailhog = new ContainerBuilder()
        .WithImage("mailhog/mailhog:latest")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ApiPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(SmtpPort).UntilPortIsAvailable(ApiPort))
        .Build();

    private readonly HttpClient _api = new();

    public async Task InitializeAsync()
    {
        await _mailhog.StartAsync();
        _api.BaseAddress = new Uri($"http://{_mailhog.Hostname}:{_mailhog.GetMappedPublicPort(ApiPort)}");
    }

    public async Task DisposeAsync()
    {
        _api.Dispose();
        await _mailhog.DisposeAsync();
    }

    [Fact(DisplayName = "FR-JOB-001/D57: gửi SMTP thật → thư multipart/alternative đúng From, To, Subject, có phần HTML và text")]
    public async Task SendAsync_DeliversMultipartMessage()
    {
        var service = new MailKitEmailService(Config(_mailhog.Hostname, _mailhog.GetMappedPublicPort(SmtpPort)));
        var message = new EmailMessage(
            ToAddress: "nguoi.nhan@example.com",
            ToName: "Người Nhận",
            Subject: "Chào mừng bạn đến với Culinary Blog!",
            HtmlBody: "<p>Xin chào <strong>Người Nhận</strong></p>",
            TextBody: "Xin chào Người Nhận");

        await service.SendAsync(message);

        var received = await ReadSingleMessageAsync();
        received.From.Mailboxes.Should().ContainSingle().Which.Address.Should().Be(FromAddress);
        received.From.Mailboxes.Single().Name.Should().Be(FromName);
        var to = received.To.Mailboxes.Should().ContainSingle().Subject;
        to.Address.Should().Be("nguoi.nhan@example.com");
        to.Name.Should().Be("Người Nhận");
        received.Subject.Should().Be("Chào mừng bạn đến với Culinary Blog!");

        received.Body.Should().BeOfType<MultipartAlternative>();
        received.HtmlBody.Should().Contain("<strong>Người Nhận</strong>");
        received.TextBody.Should().Contain("Xin chào Người Nhận");
    }

    [Fact(DisplayName = "FR-JOB-001: SMTP không kết nối được → ném exception (để Hangfire retry), không nuốt lỗi")]
    public async Task SendAsync_SmtpUnreachable_Throws()
    {
        // Cổng 1 trên localhost không có SMTP server.
        var service = new MailKitEmailService(Config("127.0.0.1", 1));
        var message = new EmailMessage("a@example.com", "A", "Tiêu đề", "<p>x</p>", "x");

        var act = () => service.SendAsync(message);

        await act.Should().ThrowAsync<Exception>();
    }

    private static IConfiguration Config(string host, int port) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smtp:Host"] = host,
                ["Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Smtp:FromAddress"] = FromAddress,
                ["Smtp:FromName"] = FromName,
            })
            .Build();

    /// <summary>GET /api/v2/messages lấy ID → GET /api/v1/messages/{id}/download lấy thư thô RFC 5322.</summary>
    private async Task<MimeMessage> ReadSingleMessageAsync()
    {
        using var list = await _api.GetFromJsonAsync<JsonDocument>("/api/v2/messages");
        var items = list!.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        var id = items[0].GetProperty("ID").GetString();

        await using var raw = await _api.GetStreamAsync($"/api/v1/messages/{id}/download");
        return await MimeMessage.LoadAsync(raw);
    }
}
