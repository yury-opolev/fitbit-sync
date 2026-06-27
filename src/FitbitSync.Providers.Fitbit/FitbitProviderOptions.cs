namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitProviderOptions
{
    public Uri BaseAddress { get; set; } = FitbitApiClient.BaseAddress;

    public string UserId { get; set; } = "-";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
