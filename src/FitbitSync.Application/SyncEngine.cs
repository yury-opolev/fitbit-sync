using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed class SyncEngine : ISyncEngine
{
    private readonly IHealthDataProvider provider;
    private readonly IMetricRepository repository;
    private readonly ISyncCheckpointStore checkpoints;
    private readonly IAuditTrail audit;
    private readonly IRateLimitGate gate;
    private readonly ISyncPlanner planner;
    private readonly ISyncResiliencePipelineProvider resilience;
    private readonly IClock clock;

    public SyncEngine(
        IHealthDataProvider provider,
        IMetricRepository repository,
        ISyncCheckpointStore checkpoints,
        IAuditTrail audit,
        IRateLimitGate gate,
        ISyncPlanner planner,
        ISyncResiliencePipelineProvider resilience,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(resilience);
        ArgumentNullException.ThrowIfNull(clock);

        this.provider = provider;
        this.repository = repository;
        this.checkpoints = checkpoints;
        this.audit = audit;
        this.gate = gate;
        this.planner = planner;
        this.resilience = resilience;
        this.clock = clock;
    }

    public async Task<SyncRunResult> RunOnceAsync(CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        await this.audit.AppendAsync("SyncStarted", CancellationToken.None).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(this.clock.UtcNow.UtcDateTime);
        var working = await this.LoadCheckpointsAsync(ct).ConfigureAwait(false);
        var plan = this.planner.PlanScheduledWork(this.provider.Capabilities, working, today);

        var result = await this.ExecutePlanAsync(runId, plan, working, ct).ConfigureAwait(false);

        await this.audit.AppendAsync("SyncCompleted", CancellationToken.None).ConfigureAwait(false);
        return result;
    }

    public async Task<SyncRunResult> RunForceSyncAsync(ForceSyncCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await this.audit.AppendAsync("ForceSync", CancellationToken.None).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(this.clock.UtcNow.UtcDateTime);
        var working = await this.LoadCheckpointsAsync(ct).ConfigureAwait(false);
        var plan = this.BuildForceSyncPlan(command, today);

        return await this.ExecutePlanAsync(command.RunId, plan, working, ct).ConfigureAwait(false);
    }

    public async Task<BackfillResult> RunBackfillAsync(BackfillCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await this.audit.AppendAsync("Backfill", CancellationToken.None).ConfigureAwait(false);

        var working = await this.LoadCheckpointsAsync(ct).ConfigureAwait(false);
        var targets = this.ResolveBackfillCapabilities(command);

        var reports = new List<MetricBackfillReport>();
        var outcome = SyncRunOutcome.Completed;

        foreach (var capability in targets)
        {
            var covered = await this.repository.GetCoveredDatesAsync(capability.Metric, command.Range, ct).ConfigureAwait(false);
            var missing = CoverageGapCalculator.MissingDates(covered, command.Range);

            var fetched = new List<DateOnly>();
            var stillMissing = new List<DateOnly>();
            var samplesWritten = 0;

            foreach (var date in missing)
            {
                if (outcome is not SyncRunOutcome.Completed || ct.IsCancellationRequested)
                {
                    if (ct.IsCancellationRequested && outcome is SyncRunOutcome.Completed)
                    {
                        outcome = SyncRunOutcome.Cancelled;
                    }

                    stillMissing.Add(date);
                    continue;
                }

                if (!this.gate.TryConsume())
                {
                    outcome = SyncRunOutcome.RateLimited;
                    stillMissing.Add(date);
                    continue;
                }

                var item = new SyncWorkItem(capability.Metric, capability.Resolution, date, SyncWorkKind.Backfill);

                try
                {
                    samplesWritten += await this.ProcessItemAsync(item, working, ct).ConfigureAwait(false);
                    fetched.Add(date);
                }
                catch (ProviderRateLimitedException ex)
                {
                    this.gate.EnterRateLimited(ex.RateLimit);
                    await this.audit.AppendAsync("RateLimited", CancellationToken.None).ConfigureAwait(false);
                    outcome = SyncRunOutcome.RateLimited;
                    stillMissing.Add(date);
                }
                catch (OperationCanceledException)
                {
                    outcome = SyncRunOutcome.Cancelled;
                    stillMissing.Add(date);
                }
                catch (Exception)
                {
                    await this.audit.AppendAsync("SyncFailed", CancellationToken.None).ConfigureAwait(false);
                    outcome = SyncRunOutcome.Faulted;
                    stillMissing.Add(date);
                }
            }

            reports.Add(new MetricBackfillReport(capability.Metric, covered, fetched, stillMissing, samplesWritten));
        }

        await this.audit.AppendAsync("BackfillCompleted", CancellationToken.None).ConfigureAwait(false);
        return new BackfillResult(command.RunId, reports, outcome);
    }

    private IReadOnlyList<MetricCapability> ResolveBackfillCapabilities(BackfillCommand command)
    {
        if (command.Metrics is not { Count: > 0 } metrics)
        {
            return this.provider.Capabilities;
        }

        var requested = metrics.ToHashSet();
        return this.provider.Capabilities.Where(capability => requested.Contains(capability.Metric)).ToList();
    }

    private IReadOnlyList<SyncWorkItem> BuildForceSyncPlan(ForceSyncCommand command, DateOnly today)
    {
        var requested = command.Metrics is { Count: > 0 } metrics ? metrics.ToHashSet() : null;
        var capabilities = requested is null
            ? this.provider.Capabilities
            : this.provider.Capabilities.Where(capability => requested.Contains(capability.Metric)).ToList();

        var items = new List<SyncWorkItem>();

        foreach (var capability in capabilities)
        {
            if (command.Range is { } range)
            {
                for (var date = range.Start; date <= range.End; date = date.AddDays(1))
                {
                    items.Add(new SyncWorkItem(capability.Metric, capability.Resolution, date, SyncWorkKind.Incremental));
                }
            }
            else
            {
                items.Add(new SyncWorkItem(capability.Metric, capability.Resolution, today, SyncWorkKind.Incremental));
            }
        }

        return items;
    }

    private async Task<SyncRunResult> ExecutePlanAsync(
        Guid runId,
        IReadOnlyList<SyncWorkItem> plan,
        Dictionary<MetricType, SyncCheckpoint?> working,
        CancellationToken ct)
    {
        var completed = 0;
        var samplesWritten = 0;
        var outcome = SyncRunOutcome.Completed;

        foreach (var item in plan)
        {
            if (ct.IsCancellationRequested)
            {
                outcome = SyncRunOutcome.Cancelled;
                break;
            }

            if (!this.gate.TryConsume())
            {
                outcome = SyncRunOutcome.RateLimited;
                break;
            }

            SyncRunOutcome? stop = null;
            var written = 0;

            try
            {
                written = await this.ProcessItemAsync(item, working, ct).ConfigureAwait(false);
            }
            catch (ProviderRateLimitedException ex)
            {
                this.gate.EnterRateLimited(ex.RateLimit);
                await this.audit.AppendAsync("RateLimited", CancellationToken.None).ConfigureAwait(false);
                stop = SyncRunOutcome.RateLimited;
            }
            catch (OperationCanceledException)
            {
                stop = SyncRunOutcome.Cancelled;
            }
            catch (Exception)
            {
                await this.audit.AppendAsync("SyncFailed", CancellationToken.None).ConfigureAwait(false);
                stop = SyncRunOutcome.Faulted;
            }

            if (stop is { } stopOutcome)
            {
                outcome = stopOutcome;
                break;
            }

            completed++;
            samplesWritten += written;
        }

        return new SyncRunResult(runId, plan.Count, completed, samplesWritten, outcome);
    }

    private async Task<int> ProcessItemAsync(SyncWorkItem item, Dictionary<MetricType, SyncCheckpoint?> working, CancellationToken ct)
    {
        var request = new MetricFetchRequest(item.Metric, item.Resolution, DateRange.SingleDay(item.Date));

        var result = await this.resilience.Pipeline
            .ExecuteAsync(async innerCt => await this.provider.FetchAsync(request, innerCt).ConfigureAwait(false), ct)
            .ConfigureAwait(false);

        if (result.RateLimit is { } snapshot)
        {
            this.gate.ApplySnapshot(snapshot);
        }

        if (result.Samples.Count > 0)
        {
            await this.repository.UpsertAsync(result.Samples, ct).ConfigureAwait(false);
        }

        await this.AdvanceCheckpointAsync(item, working, ct).ConfigureAwait(false);
        return result.Samples.Count;
    }

    private async Task AdvanceCheckpointAsync(SyncWorkItem item, Dictionary<MetricType, SyncCheckpoint?> working, CancellationToken ct)
    {
        working.TryGetValue(item.Metric, out var current);
        var checkpoint = current ?? new SyncCheckpoint(item.Metric, null, null);
        var instant = new DateTimeOffset(item.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        checkpoint = item.Kind == SyncWorkKind.Incremental
            ? checkpoint.AdvanceForward(instant)
            : checkpoint.ExtendBackfill(instant);

        working[item.Metric] = checkpoint;
        await this.checkpoints.SaveAsync(checkpoint, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<MetricType, SyncCheckpoint?>> LoadCheckpointsAsync(CancellationToken ct)
    {
        var map = new Dictionary<MetricType, SyncCheckpoint?>();

        foreach (var capability in this.provider.Capabilities)
        {
            if (!map.ContainsKey(capability.Metric))
            {
                map[capability.Metric] = await this.checkpoints.GetAsync(capability.Metric, ct).ConfigureAwait(false);
            }
        }

        return map;
    }
}
