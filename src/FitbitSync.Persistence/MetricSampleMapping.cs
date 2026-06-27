using FitbitSync.Domain;

namespace FitbitSync.Persistence;

internal static class MetricSampleMapping
{
    public static MetricSampleRow ToRow(MetricSample sample, byte[] signature, string signatureKeyId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = sample.Type,
            Timestamp = sample.Timestamp,
            Value = sample.Value,
            Unit = sample.Unit,
            Resolution = sample.Resolution,
            Source = sample.Source,
            Signature = signature,
            SignatureKeyId = signatureKeyId,
            RowVersion = Guid.NewGuid(),
        };

    public static MetricSample ToDomain(MetricSampleRow row) =>
        new(row.Type, row.Timestamp, row.Value, row.Unit, row.Resolution, row.Source);
}
