namespace AgentDispatcher.Domain.Routing;

public sealed record ExecutionRoute
{
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

        if (string.IsNullOrWhiteSpace(reasoningEffort) || reasoningEffort.Trim().Length > 32)
        {
            throw new ArgumentException(
                "推論レベルは1文字以上32文字以下で指定してください。",
                nameof(reasoningEffort));
        }

        return new ExecutionRoute(modelIdentifier.Trim(), reasoningEffort.Trim().ToLowerInvariant());
    }
}

public sealed record RoutingDecision(
    ExecutionRoute Route,
    Guid? MatchedRuleId);
