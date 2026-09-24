namespace AgentDispatcher.Domain.Routing;

public static class RoutingResolver
{
    public static RoutingDecision Resolve(
        ExecutionRoute defaultRoute,
        IEnumerable<RoutingRule> rules,
        IEnumerable<string> issueLabels)
    {
        ArgumentNullException.ThrowIfNull(defaultRoute);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(issueLabels);

        var labels = new HashSet<string>(
            issueLabels.Where(label => !string.IsNullOrWhiteSpace(label)),
            StringComparer.OrdinalIgnoreCase);

        var matched = rules
            .Where(rule => rule.Enabled)
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault(rule =>
                rule.RequiredLabels.All(labels.Contains) &&
                rule.ExcludedLabels.All(label => !labels.Contains(label)));

        return matched is null
            ? new RoutingDecision(defaultRoute, null)
            : new RoutingDecision(matched.Route, matched.Id);
    }
}
