using AgentDispatcher.Domain.Projects;

namespace AgentDispatcher.Domain.GitHub;

public sealed record IssueCandidate(
    int Number,
    string Title,
    IReadOnlyList<string> Labels,
    string Url);

public sealed record RepositoryCheckResult(
    bool Success,
    string? DefaultBranch,
    string? Error);

public sealed record IssueSearchResult(
    bool Success,
    IReadOnlyList<IssueCandidate> Issues,
    string? Error)
{
    public static IssueSearchResult Succeeded(IReadOnlyList<IssueCandidate> issues) =>
        new(true, issues, null);

    public static IssueSearchResult Failed(string error) =>
        new(false, [], error);
}

public sealed record IssueLookupResult(
    bool Success,
    IssueCandidate? Issue,
    string? Error)
{
    public static IssueLookupResult Succeeded(IssueCandidate issue) =>
        new(true, issue, null);

    public static IssueLookupResult Failed(string error) =>
        new(false, null, error);
}

public interface IGitHubIssueSource
{
    Task<RepositoryCheckResult> CheckRepositoryAsync(
        Project project,
        CancellationToken cancellationToken = default);

    Task<IssueSearchResult> SearchIssuesAsync(
        Project project,
        IssueSelectorSettings selector,
        CancellationToken cancellationToken = default);

    Task<IssueLookupResult> GetIssueAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default);
}
