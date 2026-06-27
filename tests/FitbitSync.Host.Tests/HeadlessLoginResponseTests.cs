using FluentAssertions;

namespace FitbitSync.Host.Tests;

// The headless `login --complete` outcome -> JSON-envelope contract. Success is exit 0 with only an
// `authorized` flag (never tokens/secrets); every operational failure is exit 2 with a stable error code.
public sealed class HeadlessLoginResponseTests
{
    [Fact]
    public void Authorized_IsSuccessExitZero()
    {
        var response = HeadlessLoginResponse.ForCompletion(LoginCompletionStatus.Authorized);

        response.Ok.Should().BeTrue();
        response.ExitCode.Should().Be(AgentExitCode.Success);
        response.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(LoginCompletionStatus.NoPendingAuthorization, "no_pending_authorization")]
    [InlineData(LoginCompletionStatus.AuthorizationExpired, "authorization_expired")]
    [InlineData(LoginCompletionStatus.InvalidRedirect, "invalid_redirect")]
    [InlineData(LoginCompletionStatus.AuthorizationDenied, "authorization_denied")]
    [InlineData(LoginCompletionStatus.StateMismatch, "state_mismatch")]
    [InlineData(LoginCompletionStatus.TokenExchangeFailed, "token_exchange_failed")]
    public void Failures_AreOperationFailedWithStableErrorCode(LoginCompletionStatus status, string expectedCode)
    {
        var response = HeadlessLoginResponse.ForCompletion(status);

        response.Ok.Should().BeFalse();
        response.ExitCode.Should().Be(AgentExitCode.OperationFailed);
        response.Error.Should().NotBeNull();
        response.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void SuccessPayload_NeverEmitsTokensOrSecrets()
    {
        var json = AgentJson.Serialize(HeadlessLoginResponse.ForCompletion(LoginCompletionStatus.Authorized));

        json.Should().Contain("authorized");
        json.ToLowerInvariant().Should().NotContain("token");
        json.ToLowerInvariant().Should().NotContain("verifier");
        json.ToLowerInvariant().Should().NotContain("secret");
    }
}
