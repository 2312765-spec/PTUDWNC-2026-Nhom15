using System.Diagnostics.Metrics;

namespace CulinaryBlog.Application.Common.Observability;

/// <summary>
/// FR-OBS-003/D39 — metric nghiệp vụ "recipe created/published count".
/// Chỉ dùng <see cref="System.Diagnostics.Metrics"/> (BCL) — OpenTelemetry thu thập meter này ở tầng API.
/// Handler FR-RCP-003/005 gọi sau khi <c>SaveChangesAsync</c> thành công.
/// </summary>
public sealed class RecipeMetrics
{
    public const string MeterName = "CulinaryBlog.Recipes";
    public const string CreatedCounterName = "culinaryblog.recipes.created";
    public const string PublishedCounterName = "culinaryblog.recipes.published";

    private readonly Counter<long> _created;
    private readonly Counter<long> _published;

    public RecipeMetrics(IMeterFactory meterFactory)
    {
        // Meter do IMeterFactory sở hữu (dispose cùng container) — không tự dispose ở đây.
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>(CreatedCounterName, unit: "{recipe}", description: "Số recipe được tạo.");
        _published = meter.CreateCounter<long>(PublishedCounterName, unit: "{recipe}", description: "Số recipe được publish.");
    }

    public void RecordCreated() => _created.Add(1);

    public void RecordPublished() => _published.Add(1);
}
