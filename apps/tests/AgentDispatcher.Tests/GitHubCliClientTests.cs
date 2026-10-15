using AgentDispatcher.Domain.Projects;
using AgentDispatcher.Infrastructure.GitHub;
using AgentDispatcher.Infrastructure.Processes;

namespace AgentDispatcher.Tests;

public sealed class GitHubCliClientTests
{
    [Fact]
    public async Task SearchUsesStructuredArgumentsAndFiltersPullRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processRunner = new RecordingProcessRunner(
            new ProcessResult(
                0,
                """
                [
                  {
                    "number": 12,
                    "title": "対象Issue",
                    "body": "## 目的\n\n実装する。",
                    "labels": [{"name": "agent"}],
                    "url": "https://github.com/owner/repository/issues/12",
                    "isPullRequest": false,
                    "state": "open"
                  },
                  {
                    "number": 13,
                    "title": "Pull Request",
                    "body": "",
                    "labels": [],
                    "url": "https://github.com/owner/repository/pull/13",
                    "isPullRequest": true,
                    "state": "open"
                  }
                ]
                """,
                string.Empty));

        var client = new GitHubCliClient(processRunner);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);
        var selector = IssueSelectorSettings.Create(
            project.Id,
            "-label:blocked label:agent",
            "updated",
            "asc",
            25);

        var result = await client.SearchIssuesAsync(
            project,
            selector,
            cancellationToken);

        Assert.True(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(12, issue.Number);
        Assert.Equal(["agent"], issue.Labels);
        Assert.Contains("実装する", issue.Body);

        Assert.Equal("gh", processRunner.FileName);
        Assert.Collection(
            processRunner.Arguments,
            value => Assert.Equal("search", value),
            value => Assert.Equal("issues", value),
            value => Assert.Equal("--repo", value),
            value => Assert.Equal("owner/repository", value),
            value => Assert.Equal("--state", value),
            value => Assert.Equal("open", value),
            value => Assert.Equal("--sort", value),
            value => Assert.Equal("updated", value),
            value => Assert.Equal("--order", value),
            value => Assert.Equal("asc", value),
            value => Assert.Equal("--limit", value),
            value => Assert.Equal("25", value),
            value => Assert.Equal("--json", value),
            value => Assert.Equal("number,title,body,labels,url,isPullRequest,state", value),
            value => Assert.Equal("--", value),
            value => Assert.Equal("-label:blocked label:agent", value));
    }

    [Fact]
    public async Task ProjectSearchUsesProjectQueryAndLoadsIssueBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processRunner = new SequenceProcessRunner(
            new ProcessResult(
                0,
                """
                {
                  "items": [
                    {
                      "repository": "owner/repository",
                      "content": {
                        "number": 42,
                        "type": "Issue",
                        "url": "https://github.com/owner/repository/issues/42"
                      }
                    },
                    {
                      "repository": "owner/other",
                      "content": {
                        "number": 99,
                        "type": "Issue",
                        "url": "https://github.com/owner/other/issues/99"
                      }
                    }
                  ],
                  "totalCount": 2
                }
                """,
                string.Empty),
            new ProcessResult(
                0,
                """
                {
                  "number": 42,
                  "title": "Codex対象",
                  "body": "## Codex Metadata\n\nModel: test-model\nReasoning level: high",
                  "labels": [{"name": "codex"}],
                  "url": "https://github.com/owner/repository/issues/42",
                  "state": "OPEN"
                }
                """,
                string.Empty));

        var client = new GitHubCliClient(processRunner);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            2,
            30,
            7);
        var selector = IssueSelectorSettings.Create(
            project.Id,
            "status:Ready label:codex is:issue is:open",
            "updated",
            "asc",
            25,
            "owner",
            1);

        var result = await client.SearchIssuesAsync(
            project,
            selector,
            cancellationToken);

        Assert.True(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(42, issue.Number);
        Assert.Equal(["codex"], issue.Labels);
        Assert.Contains("Model: test-model", issue.Body);

        Assert.Equal(2, processRunner.Calls.Count);
        Assert.Equal(
            [
                "project", "item-list", "1", "--owner", "owner", "--limit", "25",
                "--format", "json", "--query", "status:Ready label:codex is:issue is:open"
            ],
            processRunner.Calls[0].Arguments);
        Assert.Equal(
            [
                "issue", "view", "42", "--repo", "owner/repository", "--json",
                "number,title,body,labels,url,state"
            ],
            processRunner.Calls[1].Arguments);
    }

    [Fact]
    public async Task RepositoryCheckReturnsFailureWithoutThrowingWhenGhFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var processRunner = new RecordingProcessRunner(
            new ProcessResult(1, string.Empty, "authentication required"));
        var client = new GitHubCliClient(processRunner);
        var project = Project.Create(
            "テスト",
            "owner/repository",
            true,
            "main",
            5,
            1,
            30,
            7);

        var result = await client.CheckRepositoryAsync(
            project,
            cancellationToken);

        Assert.False(result.Success);
        Assert.Contains("authentication required", result.Error);
    }

    private sealed class RecordingProcessRunner(ProcessResult result) : IWorkerProcessRunner
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments.ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class SequenceProcessRunner(params ProcessResult[] results) : IWorkerProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);

        public List<ProcessCall> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new ProcessCall(fileName, arguments.ToArray()));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed record ProcessCall(
        string FileName,
        IReadOnlyList<string> Arguments);
}
