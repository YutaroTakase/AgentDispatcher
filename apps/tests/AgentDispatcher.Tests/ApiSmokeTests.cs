using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentDispatcher.Tests;

public sealed class ApiSmokeTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Api.Tests",
        Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ExecutionHistoryEndpointReturnsSuccess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("AgentDispatcher:DataDirectory", _directory);
                builder.UseSetting("AgentDispatcher:WorkerUser", "test-worker");
            });

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/executions?limit=10",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Equal("[]", json);
    }
}
