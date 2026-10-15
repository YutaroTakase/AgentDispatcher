using System.Text.RegularExpressions;

namespace AgentDispatcher.Domain.Projects;

public sealed class IssueSelectorSettings
{
    private static readonly HashSet<string> AllowedSortFields = new(
        ["created", "updated", "comments", "interactions", "reactions"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly Regex ProjectOwnerPattern = new(
        @"^[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private IssueSelectorSettings(
        Guid projectId,
        string queryFragment,
        string sortField,
        string sortOrder,
        int maxCandidatesPerScan,
        string? gitHubProjectOwner,
        int? gitHubProjectNumber)
    {
        ProjectId = projectId;
        QueryFragment = queryFragment;
        SortField = sortField;
        SortOrder = sortOrder;
        MaxCandidatesPerScan = maxCandidatesPerScan;
        GitHubProjectOwner = gitHubProjectOwner;
        GitHubProjectNumber = gitHubProjectNumber;
    }

    public Guid ProjectId { get; }

    public string QueryFragment { get; }

    public string SortField { get; }

    public string SortOrder { get; }

    public int MaxCandidatesPerScan { get; }

    public string? GitHubProjectOwner { get; }

    public int? GitHubProjectNumber { get; }

    public bool UsesGitHubProject =>
        !string.IsNullOrWhiteSpace(GitHubProjectOwner) && GitHubProjectNumber.HasValue;

    public static IssueSelectorSettings Create(
        Guid projectId,
        string? queryFragment,
        string sortField,
        string sortOrder,
        int maxCandidatesPerScan,
        string? gitHubProjectOwner = null,
        int? gitHubProjectNumber = null)
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

        var normalizedProjectOwner = string.IsNullOrWhiteSpace(gitHubProjectOwner)
            ? null
            : gitHubProjectOwner.Trim();

        if ((normalizedProjectOwner is null) != (gitHubProjectNumber is null))
        {
            throw new ArgumentException(
                "GitHub Project を使用する場合は owner と project number の両方を指定してください。",
                nameof(gitHubProjectOwner));
        }

        if (normalizedProjectOwner is not null &&
            (!ProjectOwnerPattern.IsMatch(normalizedProjectOwner) || normalizedProjectOwner.Length > 100))
        {
            throw new ArgumentException(
                "GitHub Project owner はGitHubのowner名として有効な100文字以下の値を指定してください。",
                nameof(gitHubProjectOwner));
        }

        if (gitHubProjectNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gitHubProjectNumber),
                "GitHub Project number は1以上で指定してください。");
        }

        return new IssueSelectorSettings(
            projectId,
            normalizedQuery,
            sortField.Trim().ToLowerInvariant(),
            normalizedOrder,
            maxCandidatesPerScan,
            normalizedProjectOwner,
            gitHubProjectNumber);
    }
}
