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
                    "labels": [{"name": "agent"}],
                    "url": "https://github.com/owner/repository/issues/12",
                    "isPullRequest": false,
                    "state": "open"
                  },
                  {
                    "number": 13,
                    "title": "Pull Request",
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
            value => Assert.Equal("number,title,labels,url,isPullRequest,state", value),
            value => Assert.Equal("--", value),
            value => Assert.Equal("-label:blocked label:agent", value));
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

    private sealed class RecordingProcessRunner(ProcessResult result) : IProcessRunner
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
}
