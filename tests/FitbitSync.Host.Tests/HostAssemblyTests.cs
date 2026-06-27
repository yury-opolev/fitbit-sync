using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 6a smoke: the Host assembly is referenced and reachable from the test project.
public sealed class HostAssemblyTests
{
    [Fact]
    public void HostAssembly_IsReachable()
    {
        var assembly = typeof(Program).Assembly;

        assembly.GetName().Name.Should().Be("FitbitSync.Host");
    }
}
