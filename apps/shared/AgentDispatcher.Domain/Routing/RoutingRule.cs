namespace AgentDispatcher.Domain.Routing;

public sealed class RoutingRule
{
    private RoutingRule(
        Guid id,
        Guid projectId,
        int order,
        bool enabled,
        IReadOnlyList<string> requiredLabels,
        IReadOnlyList<string> excludedLabels,
        ExecutionRoute route)
    {
        Id = id;
        ProjectId = projectId;
        Order = order;
        Enabled = enabled;
        RequiredLabels = requiredLabels;
        ExcludedLabels = excludedLabels;
        Route = route;
    }

    public Guid Id { get; }

    public Guid ProjectId { get; }

    public int Order { get; }

    public bool Enabled { get; }

    public IReadOnlyList<string> RequiredLabels { get; }

    public IReadOnlyList<string> ExcludedLabels { get; }

    public ExecutionRoute Route { get; }

    public static RoutingRule Create(
        Guid projectId,
        int order,
        bool enabled,
        IEnumerable<string>? requiredLabels,
        IEnumerable<string>? excludedLabels,
        ExecutionRoute route)
    {
        return Restore(
            Guid.NewGuid(),
            projectId,
            order,
            enabled,
            requiredLabels,
            excludedLabels,
            route);
    }

    public static RoutingRule Restore(
        Guid id,
        Guid projectId,
        int order,
        bool enabled,
        IEnumerable<string>? requiredLabels,
        IEnumerable<string>? excludedLabels,
        ExecutionRoute route)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("規則IDは必須です。", nameof(id));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("プロジェクトIDは必須です。", nameof(projectId));
        }

        if (order is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "評価順は0以上10000以下で指定してください。");
        }

        ArgumentNullException.ThrowIfNull(route);

        var required = NormalizeLabels(requiredLabels, nameof(requiredLabels));
        var excluded = NormalizeLabels(excludedLabels, nameof(excludedLabels));

        return new RoutingRule(id, projectId, order, enabled, required, excluded, route);
    }

    private static IReadOnlyList<string> NormalizeLabels(
        IEnumerable<string>? labels,
        string parameterName)
    {
        var normalized = (labels ?? [])
            .Select(label => label?.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length > 20)
        {
            throw new ArgumentException("ラベル条件は20件以下で指定してください。", parameterName);
        }

        if (normalized.Any(label => label.Length > 100))
        {
            throw new ArgumentException("ラベル名は100文字以下で指定してください。", parameterName);
        }

        return normalized;
    }
}
