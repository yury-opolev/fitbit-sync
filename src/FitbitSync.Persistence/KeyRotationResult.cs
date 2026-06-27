namespace FitbitSync.Persistence;

public sealed record KeyRotationResult(
    int ResignedSampleCount,
    string NewSigningKeyId,
    bool DatabaseRekeyed);
