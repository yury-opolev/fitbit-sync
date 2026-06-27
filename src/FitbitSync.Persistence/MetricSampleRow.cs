using FitbitSync.Domain;

namespace FitbitSync.Persistence;

public sealed class MetricSampleRow
{
    public Guid Id { get; set; }

    public MetricType Type { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public double Value { get; set; }

    public string Unit { get; set; } = string.Empty;

    public IntradayResolution Resolution { get; set; }

    public string Source { get; set; } = string.Empty;

    public byte[] Signature { get; set; } = [];

    public string SignatureKeyId { get; set; } = string.Empty;

    public Guid RowVersion { get; set; }
}
