using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Domain.Routing;
using AgentDispatcher.Infrastructure.Persistence;

namespace AgentDispatcher.Tests;

public sealed class AutomationSettingsPersistenceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "AgentDispatcher.Tests",
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
    public async Task SelectorAndRoutingSettingsPersistAndCascadeWithProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = SqliteDatabase.FromFile(
            Path.Combine(_directory, "agent-dispatcher.db"));
        await database.InitializeAsync(cancellationToken);

        var projects = new SqliteProjectRepository(database);
        var selectors = new SqliteIssueSelectorRepository(database);
        var routing = new SqliteRoutingConfigurationRepository(database);

        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);
        await projects.AddAsync(project, cancellationToken);

        var selector = IssueSelectorSettings.Create(
            project.Id,
            "label:agent -label:blocked",
            "updated",
            "desc",
            20);
        await selectors.UpsertAsync(selector, cancellationToken);

        var defaultRoute = ExecutionRoute.Create("default-model", "medium");
        await routing.UpsertDefaultRouteAsync(
            project.Id,
            defaultRoute,
            cancellationToken);

        var rule = RoutingRule.Create(
            project.Id,
            10,
            true,
            ["architecture"],
            ["blocked"],
            ExecutionRoute.Create("architecture-model", "high"));
        await routing.ReplaceRulesAsync(
            project.Id,
            [rule],
            cancellationToken);

        var restoredSelector = await selectors.GetAsync(
            project.Id,
            cancellationToken);
        var restoredDefault = await routing.GetDefaultRouteAsync(
            project.Id,
            cancellationToken);
        var restoredRules = await routing.ListRulesAsync(
            project.Id,
            cancellationToken);

        Assert.NotNull(restoredSelector);
        Assert.Equal(selector.QueryFragment, restoredSelector.QueryFragment);
        Assert.Equal(defaultRoute, restoredDefault);

        var restoredRule = Assert.Single(restoredRules);
        Assert.Equal(rule.Id, restoredRule.Id);
        Assert.Equal(["architecture"], restoredRule.RequiredLabels);
        Assert.Equal(["blocked"], restoredRule.ExcludedLabels);
        Assert.Equal("architecture-model", restoredRule.Route.ModelIdentifier);

        Assert.True(await projects.DeleteAsync(project.Id, cancellationToken));
        Assert.Null(await selectors.GetAsync(project.Id, cancellationToken));
        Assert.Null(await routing.GetDefaultRouteAsync(project.Id, cancellationToken));
        Assert.Empty(await routing.ListRulesAsync(project.Id, cancellationToken));
    }
}
