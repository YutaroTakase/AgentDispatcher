namespace AgentDispatcher.Domain.Routing;

public sealed record ExecutionRoute
{
    private static readonly HashSet<string> AllowedReasoningEfforts = new(
        ["minimal", "low", "medium", "high", "xhigh"],
        StringComparer.OrdinalIgnoreCase);

    private ExecutionRoute(string modelIdentifier, string reasoningEffort)
    {
        ModelIdentifier = modelIdentifier;
        ReasoningEffort = reasoningEffort;
    }

    public string ModelIdentifier { get; }

    public string ReasoningEffort { get; }

    public static ExecutionRoute Create(string modelIdentifier, string reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(modelIdentifier) || modelIdentifier.Trim().Length > 100)
        {
            throw new ArgumentException(
                "推論モデル識別子は1文字以上100文字以下で指定してください。",
                nameof(modelIdentifier));
        }

        var normalizedEffort = reasoningEffort?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEffort) ||
            !AllowedReasoningEfforts.Contains(normalizedEffort))
        {
            throw new ArgumentException(
                "推論レベルは minimal、low、medium、high、xhigh のいずれかで指定してください。",
                nameof(reasoningEffort));
        }

        return new ExecutionRoute(modelIdentifier.Trim(), normalizedEffort);
    }
}
public sealed record RoutingDecision(
    ExecutionRoute Route,
    Guid? MatchedRuleId);
