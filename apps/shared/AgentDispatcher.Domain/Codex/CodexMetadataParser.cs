using AgentDispatcher.Domain.Routing;

namespace AgentDispatcher.Domain.Codex;

public enum CodexMetadataState
{
    NotPresent,
    Valid,
    Invalid
}

public sealed record CodexMetadataParseResult(
    CodexMetadataState State,
    ExecutionRoute? Route,
    string? Error)
{
    public static CodexMetadataParseResult NotPresent() =>
        new(CodexMetadataState.NotPresent, null, null);

    public static CodexMetadataParseResult Valid(ExecutionRoute route) =>
        new(CodexMetadataState.Valid, route, null);

    public static CodexMetadataParseResult Invalid(string error) =>
        new(CodexMetadataState.Invalid, null, error);
}

public static class CodexMetadataParser
{
    private const string Heading = "## Codex Metadata";

    public static CodexMetadataParseResult Parse(string? issueBody)
    {
        if (string.IsNullOrWhiteSpace(issueBody))
        {
            return CodexMetadataParseResult.NotPresent();
        }

        var normalized = issueBody
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimStart('\uFEFF', ' ', '\t', '\n');

        var metadataIndex = normalized.IndexOf(Heading, StringComparison.Ordinal);
        if (metadataIndex < 0)
        {
            return CodexMetadataParseResult.NotPresent();
        }

        if (metadataIndex != 0)
        {
            return CodexMetadataParseResult.Invalid(
                "Codex Metadata は Issue 本文の先頭に配置してください。");
        }

        var sectionEnd = normalized.IndexOf("\n## ", Heading.Length, StringComparison.Ordinal);
        var section = sectionEnd < 0
            ? normalized
            : normalized[..sectionEnd];

        string? model = null;
        string? reasoningLevel = null;

        foreach (var rawLine in section.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Model:", StringComparison.OrdinalIgnoreCase))
            {
                model = NormalizeValue(line["Model:".Length..]);
                continue;
            }

            if (line.StartsWith("Reasoning level:", StringComparison.OrdinalIgnoreCase))
            {
                reasoningLevel = NormalizeValue(line["Reasoning level:".Length..]);
            }
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return CodexMetadataParseResult.Invalid(
                "Codex Metadata に Model が設定されていません。");
        }

        if (string.IsNullOrWhiteSpace(reasoningLevel))
        {
            return CodexMetadataParseResult.Invalid(
                "Codex Metadata に Reasoning level が設定されていません。");
        }

        try
        {
            return CodexMetadataParseResult.Valid(
                ExecutionRoute.Create(model, reasoningLevel));
        }
        catch (ArgumentException exception)
        {
            return CodexMetadataParseResult.Invalid(exception.Message);
        }
    }

    private static string NormalizeValue(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length >= 2 &&
            normalized.StartsWith('`') &&
            normalized.EndsWith('`'))
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }
}
