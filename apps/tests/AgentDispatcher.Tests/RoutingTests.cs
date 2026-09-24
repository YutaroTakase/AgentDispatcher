using AgentDispatcher.Domain.Routing;

namespace AgentDispatcher.Tests;

public sealed class RoutingTests
{
    [Fact]
    public void DefaultRouteIsUsedWhenNoRuleMatches()
    {
        var projectId = Guid.NewGuid();
        var defaultRoute = ExecutionRoute.Create("default-model", "medium");
        var rule = RoutingRule.Create(
            projectId,
            10,
            true,
            ["architecture"],
            [],
            ExecutionRoute.Create("architecture-model", "high"));

        var decision = RoutingResolver.Resolve(
            defaultRoute,
            [rule],
            ["bug"]);

        Assert.Equal(defaultRoute, decision.Route);
        Assert.Null(decision.MatchedRuleId);
    }

    [Fact]
    public void FirstMatchingRuleByOrderIsUsed()
    {
        var projectId = Guid.NewGuid();
        var defaultRoute = ExecutionRoute.Create("default-model", "medium");
        var laterRule = RoutingRule.Create(
            projectId,
            20,
            true,
            ["agent"],
            [],
            ExecutionRoute.Create("later-model", "high"));
        var firstRule = RoutingRule.Create(
            projectId,
            10,
            true,
            ["AGENT"],
            ["blocked"],
            ExecutionRoute.Create("first-model", "medium"));

        var decision = RoutingResolver.Resolve(
            defaultRoute,
            [laterRule, firstRule],
            ["agent", "ready"]);

        Assert.Equal(firstRule.Id, decision.MatchedRuleId);
        Assert.Equal("first-model", decision.Route.ModelIdentifier);
    }

    [Fact]
    public void ExcludedLabelPreventsMatch()
    {
        var projectId = Guid.NewGuid();
        var defaultRoute = ExecutionRoute.Create("default-model", "medium");
        var rule = RoutingRule.Create(
            projectId,
            0,
            true,
            ["agent"],
            ["blocked"],
            ExecutionRoute.Create("other-model", "high"));

        var decision = RoutingResolver.Resolve(
            defaultRoute,
            [rule],
            ["agent", "BLOCKED"]);

        Assert.Equal(defaultRoute, decision.Route);
    }

    [Fact]
    public void DisabledRuleIsIgnored()
    {
        var projectId = Guid.NewGuid();
        var defaultRoute = ExecutionRoute.Create("default-model", "medium");
        var rule = RoutingRule.Create(
            projectId,
            0,
            false,
            [],
            [],
            ExecutionRoute.Create("other-model", "high"));

        var decision = RoutingResolver.Resolve(defaultRoute, [rule], []);

        Assert.Equal(defaultRoute, decision.Route);
    }
}
