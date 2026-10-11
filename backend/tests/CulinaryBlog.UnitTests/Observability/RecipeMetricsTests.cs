using System.Diagnostics.Metrics;
using CulinaryBlog.Application.Common.Observability;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Xunit;

namespace CulinaryBlog.UnitTests.Observability;

public sealed class RecipeMetricsTests : IDisposable
{
    private readonly TestMeterFactory _meterFactory = new();

    [Fact(DisplayName = "FR-OBS-003/D39: RecordCreated tăng counter recipes.created đúng 1")]
    public void RecordCreated_IncrementsCreatedCounter()
    {
        using var collector = new MetricCollector<long>(
            _meterFactory, RecipeMetrics.MeterName, RecipeMetrics.CreatedCounterName);
        var sut = new RecipeMetrics(_meterFactory);

        sut.RecordCreated();
        sut.RecordCreated();

        collector.GetMeasurementSnapshot().Select(m => m.Value).Should().Equal(1, 1);
    }

    [Fact(DisplayName = "FR-OBS-003/D39: RecordPublished tăng counter recipes.published, không đụng created")]
    public void RecordPublished_IncrementsPublishedCounterOnly()
    {
        using var published = new MetricCollector<long>(
            _meterFactory, RecipeMetrics.MeterName, RecipeMetrics.PublishedCounterName);
        using var created = new MetricCollector<long>(
            _meterFactory, RecipeMetrics.MeterName, RecipeMetrics.CreatedCounterName);
        var sut = new RecipeMetrics(_meterFactory);

        sut.RecordPublished();

        published.GetMeasurementSnapshot().Select(m => m.Value).Should().Equal(1);
        created.GetMeasurementSnapshot().Should().BeEmpty();
    }

    public void Dispose() => _meterFactory.Dispose();

    /// <summary>
    /// IMeterFactory tối giản: gắn Scope = chính nó để MetricCollector chỉ nghe meter tạo từ đây
    /// (không lẫn với meter cùng tên của test khác chạy song song).
    /// </summary>
    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(new MeterOptions(options.Name)
            {
                Version = options.Version,
                Tags = options.Tags,
                Scope = this,
            });
            _meters.Add(meter);
            return meter;
        }

        public void Dispose() => _meters.ForEach(m => m.Dispose());
    }
}
