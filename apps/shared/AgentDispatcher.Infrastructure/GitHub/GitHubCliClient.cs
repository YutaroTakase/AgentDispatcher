using System.Text.Json;
using AgentDispatcher.Domain.GitHub;
using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Infrastructure.GitHub;

public sealed class GitHubCliClient(IWorkerProcessRunner processRunner) : IGitHubIssueSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<RepositoryCheckResult> CheckRepositoryAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var repositoryResult = await processRunner.RunAsync(
            "gh",
            [
                "repo",
                "view",
                project.Repository,
                "--json",
                "nameWithOwner"
            ],
            cancellationToken: cancellationToken);

        if (repositoryResult.ExitCode != 0)
        {
            return new RepositoryCheckResult(
                false,
                null,
                NormalizeError(repositoryResult.StandardError));
        }

        try
        {
            var response = JsonSerializer.Deserialize<RepositoryResponse>(
                repositoryResult.StandardOutput,
                JsonOptions);

            if (response is null ||
                !string.Equals(
                    response.NameWithOwner,
                    project.Repository,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new RepositoryCheckResult(
                    false,
                    null,
                    "GitHubから対象リポジトリを確認できませんでした。");
            }
        }
        catch (JsonException)
        {
            return new RepositoryCheckResult(
                false,
                null,
                "GitHub CLIのリポジトリ確認結果を解析できませんでした。");
        }

        var branchResult = await processRunner.RunAsync(
            "gh",
            [
                "api",
                $"repos/{project.Repository}/branches/{Uri.EscapeDataString(project.DefaultBranch)}",
                "--jq",
                ".name"
            ],
            cancellationToken: cancellationToken);

        if (branchResult.ExitCode != 0)
        {
            return new RepositoryCheckResult(
                false,
                null,
                NormalizeError(branchResult.StandardError));
        }

        var branchName = branchResult.StandardOutput.Trim();
        if (!string.Equals(
                branchName,
                project.DefaultBranch,
                StringComparison.Ordinal))
        {
            return new RepositoryCheckResult(
                false,
                null,
                "設定された既定ブランチをGitHub上で確認できませんでした。");
        }

        return new RepositoryCheckResult(true, branchName, null);
    }

    public Task<IssueSearchResult> SearchIssuesAsync(
        Project project,
        IssueSelectorSettings selector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(selector);

        if (project.Id != selector.ProjectId)
        {
            throw new ArgumentException("Issue取得設定のプロジェクトが一致しません。", nameof(selector));
        }

        return selector.UsesGitHubProject
            ? SearchProjectIssuesAsync(project, selector, cancellationToken)
            : SearchRepositoryIssuesAsync(project, selector, cancellationToken);
    }

    public async Task<IssueLookupResult> GetIssueAsync(
        Project project,
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (issueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(issueNumber));
        }

        var result = await processRunner.RunAsync(
            "gh",
            [
                "issue",
                "view",
                issueNumber.ToString(),
                "--repo",
                project.Repository,
                "--json",
                "number,title,body,labels,url,state"
            ],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return IssueLookupResult.Failed(NormalizeError(result.StandardError));
        }

        try
        {
            var issue = JsonSerializer.Deserialize<IssueViewResponse>(
                result.StandardOutput,
                JsonOptions);

            if (issue is null ||
                !string.Equals(issue.State, "open", StringComparison.OrdinalIgnoreCase))
            {
                return IssueLookupResult.Failed("対象Issueが存在しないか、未完了ではありません。");
            }

            return IssueLookupResult.Succeeded(ToCandidate(issue));
        }
        catch (JsonException)
        {
            return IssueLookupResult.Failed(
                "GitHub CLIのIssue取得結果を解析できませんでした。");
        }
    }

    private async Task<IssueSearchResult> SearchRepositoryIssuesAsync(
        Project project,
        IssueSelectorSettings selector,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "search",
            "issues",
            "--repo",
            project.Repository,
            "--state",
            "open",
            "--sort",
            selector.SortField,
            "--order",
            selector.SortOrder,
            "--limit",
            selector.MaxCandidatesPerScan.ToString(),
            "--json",
            "number,title,body,labels,url,isPullRequest,state"
        };

        if (!string.IsNullOrWhiteSpace(selector.QueryFragment))
        {
            arguments.Add("--");
            arguments.Add(selector.QueryFragment);
        }

        var result = await processRunner.RunAsync(
            "gh",
            arguments,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return IssueSearchResult.Failed(NormalizeError(result.StandardError));
        }

        try
        {
            var response = JsonSerializer.Deserialize<IssueResponse[]>(
                result.StandardOutput,
                JsonOptions) ?? [];

            var issues = response
                .Where(issue =>
                    !issue.IsPullRequest &&
                    string.Equals(issue.State, "open", StringComparison.OrdinalIgnoreCase))
                .Select(ToCandidate)
                .ToArray();

            return IssueSearchResult.Succeeded(issues);
        }
        catch (JsonException)
        {
            return IssueSearchResult.Failed(
                "GitHub CLIのIssue検索結果を解析できませんでした。");
        }
    }

    private async Task<IssueSearchResult> SearchProjectIssuesAsync(
        Project project,
        IssueSelectorSettings selector,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "project",
            "item-list",
            selector.GitHubProjectNumber!.Value.ToString(),
            "--owner",
            selector.GitHubProjectOwner!,
            "--limit",
            selector.MaxCandidatesPerScan.ToString(),
            "--format",
            "json"
        };

        if (!string.IsNullOrWhiteSpace(selector.QueryFragment))
        {
            arguments.Add("--query");
            arguments.Add(selector.QueryFragment);
        }

        var result = await processRunner.RunAsync(
            "gh",
            arguments,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return IssueSearchResult.Failed(NormalizeError(result.StandardError));
        }

        ProjectItemsResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<ProjectItemsResponse>(
                result.StandardOutput,
                JsonOptions);
        }
        catch (JsonException)
        {
            return IssueSearchResult.Failed(
                "GitHub CLIのProject Item取得結果を解析できませんでした。");
        }

        var itemNumbers = (response?.Items ?? [])
            .Where(item => IsRepositoryIssue(item, project.Repository))
            .Select(item => item.Content!.Number)
            .Where(number => number > 0)
            .Distinct()
            .Take(selector.MaxCandidatesPerScan)
            .ToArray();

        var issues = new List<IssueCandidate>(itemNumbers.Length);
        foreach (var issueNumber in itemNumbers)
        {
            var lookup = await GetIssueAsync(project, issueNumber, cancellationToken);
            if (!lookup.Success || lookup.Issue is null)
            {
                return IssueSearchResult.Failed(
                    lookup.Error ?? $"Project Item の Issue #{issueNumber} を取得できませんでした。");
            }

            issues.Add(lookup.Issue);
        }

        return IssueSearchResult.Succeeded(issues);
    }

    private static bool IsRepositoryIssue(ProjectItemResponse item, string repository)
    {
        if (item.Content is null || item.Content.Number <= 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(item.Content.Type) &&
            !string.Equals(item.Content.Type, "Issue", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(item.Repository))
        {
            return string.Equals(item.Repository, repository, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(item.Content.Url) &&
            item.Content.Url.StartsWith(
                $"https://github.com/{repository}/issues/",
                StringComparison.OrdinalIgnoreCase);
    }

    private static IssueCandidate ToCandidate(IssueResponse issue) =>
        new(
            issue.Number,
            issue.Title ?? string.Empty,
            issue.Labels?
                .Where(label => !string.IsNullOrWhiteSpace(label.Name))
                .Select(label => label.Name!)
                .ToArray() ?? [],
            issue.Url ?? string.Empty,
            issue.Body ?? string.Empty);

    private static IssueCandidate ToCandidate(IssueViewResponse issue) =>
        new(
            issue.Number,
            issue.Title ?? string.Empty,
            issue.Labels?
                .Where(label => !string.IsNullOrWhiteSpace(label.Name))
                .Select(label => label.Name!)
                .ToArray() ?? [],
            issue.Url ?? string.Empty,
            issue.Body ?? string.Empty);

    private static string NormalizeError(string error)
    {
        return string.IsNullOrWhiteSpace(error)
            ? "GitHub CLIの実行に失敗しました。"
            : error.Trim();
    }

    private sealed record RepositoryResponse(string? NameWithOwner);

    private sealed record IssueResponse(
        int Number,
        string? Title,
        string? Body,
        LabelResponse[]? Labels,
        string? Url,
        bool IsPullRequest,
        string? State);

    private sealed record IssueViewResponse(
        int Number,
        string? Title,
        string? Body,
        LabelResponse[]? Labels,
        string? Url,
        string? State);

    private sealed record ProjectItemsResponse(ProjectItemResponse[]? Items);

    private sealed record ProjectItemResponse(
        string? Repository,
        ProjectItemContentResponse? Content);

    private sealed record ProjectItemContentResponse(
        int Number,
        string? Type,
        string? Url);

    private sealed record LabelResponse(string? Name);
}
