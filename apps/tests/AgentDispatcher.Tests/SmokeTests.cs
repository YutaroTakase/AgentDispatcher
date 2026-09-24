using AgentDispatcher.Domain;

namespace AgentDispatcher.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        Assert.NotNull(typeof(AssemblyMarker).Assembly);
    }
}
