namespace AgentDispatcher.Domain.Projects;

public sealed class IssueSelectorSettings
{
    private static readonly HashSet<string> AllowedSortFields = new(
        ["created", "updated", "comments", "interactions", "reactions"],
        StringComparer.OrdinalIgnoreCase);

    private IssueSelectorSettings(
        Guid projectId,
        string queryFragment,
        string sortField,
        string sortOrder,
        int maxCandidatesPerScan)
    {
        ProjectId = projectId;
        QueryFragment = queryFragment;
        SortField = sortField;
        SortOrder = sortOrder;
        MaxCandidatesPerScan = maxCandidatesPerScan;
    }

    public Guid ProjectId { get; }

    public string QueryFragment { get; }

    public string SortField { get; }

    public string SortOrder { get; }

    public int MaxCandidatesPerScan { get; }

    public static IssueSelectorSettings Create(
        Guid projectId,
        string? queryFragment,
        string sortField,
        string sortOrder,
        int maxCandidatesPerScan)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("プロジェクトIDは必須です。", nameof(projectId));
        }

        var normalizedQuery = queryFragment?.Trim() ?? string.Empty;
        if (normalizedQuery.Length > 1_000)
        {
            throw new ArgumentException("Issue検索条件は1000文字以下で指定してください。", nameof(queryFragment));
        }

        if (string.IsNullOrWhiteSpace(sortField) || !AllowedSortFields.Contains(sortField.Trim()))
        {
            throw new ArgumentException(
                "並び替え項目は created、updated、comments、interactions、reactions のいずれかで指定してください。",
                nameof(sortField));
        }

        var normalizedOrder = sortOrder.Trim().ToLowerInvariant();
        if (normalizedOrder is not ("asc" or "desc"))
        {
            throw new ArgumentException("並び順は asc または desc で指定してください。", nameof(sortOrder));
        }

        if (maxCandidatesPerScan is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCandidatesPerScan),
                "1回の確認で取得する最大候補数は1件以上100件以下で指定してください。");
        }

        return new IssueSelectorSettings(
            projectId,
            normalizedQuery,
            sortField.Trim().ToLowerInvariant(),
            normalizedOrder,
            maxCandidatesPerScan);
    }
}
