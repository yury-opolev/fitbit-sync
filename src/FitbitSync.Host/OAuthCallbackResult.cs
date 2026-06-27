namespace FitbitSync.Host;

public sealed record OAuthCallbackResult
{
    private OAuthCallbackResult(string? code, string? state, string? error)
    {
        this.Code = code;
        this.State = state;
        this.Error = error;
    }

    public string? Code { get; }

    public string? State { get; }

    public string? Error { get; }

    public bool IsSuccess => this.Error is null && this.Code is not null && this.State is not null;

    public static OAuthCallbackResult Success(string code, string state)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(state);
        return new OAuthCallbackResult(code, state, null);
    }

    public static OAuthCallbackResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);
        return new OAuthCallbackResult(null, null, error);
    }
}
