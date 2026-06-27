using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// In-memory port doubles for engine/scheduler tests. They deliberately IGNORE the CancellationToken on
// writes so a "simulated crash" (token cancelled mid-run) still durably persists the work already done —
// exactly the durability the resumability guarantee relies on.

internal sealed class InMemoryCheckpointStore : ISyncCheckpointStore
{
    private readonly Dictionary<MetricType, SyncCheckpoint> store = new();

    public int SaveCount { get; private set; }

    public Task<SyncCheckpoint?> GetAsync(MetricType metric, CancellationToken ct = default) =>
        Task.FromResult(this.store.TryGetValue(metric, out var checkpoint) ? checkpoint : null);

    public Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        this.store[checkpoint.Metric] = checkpoint;
        this.SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingMetricRepository : IMetricRepository
{
    public List<MetricSample> Upserted { get; } = [];

    public Task UpsertAsync(IReadOnlyCollection<MetricSample> samples, CancellationToken ct = default)
    {
        this.Upserted.AddRange(samples);
        return Task.CompletedTask;
    }

    public Task<SyncCheckpoint?> GetCheckpointAsync(MetricType metric, CancellationToken ct = default) =>
        Task.FromResult<SyncCheckpoint?>(null);

    public Task<IReadOnlyList<DateOnly>> GetCoveredDatesAsync(MetricType metric, DateRange range, CancellationToken ct = default)
    {
        IReadOnlyList<DateOnly> dates = this.Upserted
            .Where(sample => sample.Type == metric)
            .Select(sample => DateOnly.FromDateTime(sample.Timestamp.UtcDateTime))
            .Where(date => date >= range.Start && date <= range.End)
            .Distinct()
            .OrderBy(date => date)
            .ToList();
        return Task.FromResult(dates);
    }

    public Task<IReadOnlyList<MetricSample>> QueryAsync(MetricType metric, DateRange range, CancellationToken ct = default)
    {
        IReadOnlyList<MetricSample> samples = this.Upserted
            .Where(sample => sample.Type == metric
                && DateOnly.FromDateTime(sample.Timestamp.UtcDateTime) >= range.Start
                && DateOnly.FromDateTime(sample.Timestamp.UtcDateTime) <= range.End)
            .OrderBy(sample => sample.Timestamp)
            .ToList();
        return Task.FromResult(samples);
    }
}

internal sealed class InMemoryAuditTrail : IAuditTrail
{
    private const string GenesisHash = "GENESIS";

    private readonly List<AuditEntry> entries = [];

    public IReadOnlyList<AuditEntry> Entries => this.entries;

    public IReadOnlyList<string> Actions => this.entries.Select(entry => entry.Action).ToList();

    public Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
    {
        var sequence = this.entries.Count + 1L;
        var prevHash = this.entries.Count == 0 ? GenesisHash : this.entries[^1].EntryHash;
        var timestamp = DateTimeOffset.UnixEpoch.AddSeconds(sequence);
        var entryHash = ComputeHash(sequence, timestamp, action, prevHash);
        var entry = new AuditEntry(sequence, timestamp, action, prevHash, entryHash);
        this.entries.Add(entry);
        return Task.FromResult(entry);
    }

    public Task<bool> VerifyChainAsync(CancellationToken ct = default)
    {
        var expectedPrev = GenesisHash;

        foreach (var entry in this.entries)
        {
            if (entry.PrevHash != expectedPrev)
            {
                return Task.FromResult(false);
            }

            if (ComputeHash(entry.Sequence, entry.Timestamp, entry.Action, entry.PrevHash) != entry.EntryHash)
            {
                return Task.FromResult(false);
            }

            expectedPrev = entry.EntryHash;
        }

        return Task.FromResult(true);
    }

    private static string ComputeHash(long sequence, DateTimeOffset timestamp, string action, string prevHash)
    {
        var material = string.Create(CultureInfo.InvariantCulture, $"{sequence}|{timestamp.UtcTicks}|{action}|{prevHash}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}

internal sealed class RecordingHealthDataProvider : IHealthDataProvider
{
    private readonly Action<RecordingHealthDataProvider>? afterFetch;
    private readonly RateLimitSnapshot? rateLimit;

    public RecordingHealthDataProvider(
        IReadOnlyList<MetricCapability> capabilities,
        Action<RecordingHealthDataProvider>? afterFetch = null,
        RateLimitSnapshot? rateLimit = null)
    {
        this.Capabilities = capabilities;
        this.afterFetch = afterFetch;
        this.rateLimit = rateLimit;
    }

    public string Source => "fake";

    public IReadOnlyList<MetricCapability> Capabilities { get; }

    public List<DateOnly> FetchedDates { get; } = [];

    public Func<MetricFetchRequest, MetricFetchResult>? FetchOverride { get; set; }

    public Task<MetricFetchResult> FetchAsync(MetricFetchRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        this.FetchedDates.Add(request.Range.Start);

        if (this.FetchOverride is { } over)
        {
            var overridden = over(request);
            this.afterFetch?.Invoke(this);
            return Task.FromResult(overridden);
        }

        var sample = new MetricSample(
            request.Metric,
            new DateTimeOffset(request.Range.Start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            1,
            "unit",
            request.Resolution,
            this.Source);

        var result = new MetricFetchResult([sample], this.rateLimit);
        this.afterFetch?.Invoke(this);
        return Task.FromResult(result);
    }
}
