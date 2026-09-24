using System.Text.RegularExpressions;

namespace AgentDispatcher.Domain.Projects;

public sealed class Project
{
    private static readonly Regex RepositoryPattern = new(
        @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Project(
        Guid id,
        string displayName,
        string repository,
        bool enabled,
        string defaultBranch,
        int issueScanIntervalMinutes,
        int maxConcurrentExecutions,
        int executionRetentionDays,
        int failureWorktreeRetentionDays)
    {
        Id = id;
        DisplayName = displayName;
        Repository = repository;
        Enabled = enabled;
        DefaultBranch = defaultBranch;
        IssueScanIntervalMinutes = issueScanIntervalMinutes;
        MaxConcurrentExecutions = maxConcurrentExecutions;
        ExecutionRetentionDays = executionRetentionDays;
        FailureWorktreeRetentionDays = failureWorktreeRetentionDays;
    }

    public Guid Id { get; }

    public string DisplayName { get; }

    public string Repository { get; }

    public bool Enabled { get; }

    public string DefaultBranch { get; }

    public int IssueScanIntervalMinutes { get; }

    public int MaxConcurrentExecutions { get; }

    public int ExecutionRetentionDays { get; }

    public int FailureWorktreeRetentionDays { get; }

    public static Project Create(
        string displayName,
        string repository,
        bool enabled,
        string defaultBranch,
        int issueScanIntervalMinutes,
        int maxConcurrentExecutions,
        int executionRetentionDays,
        int failureWorktreeRetentionDays)
    {
        return Restore(
            Guid.NewGuid(),
            displayName,
            repository,
            enabled,
            defaultBranch,
            issueScanIntervalMinutes,
            maxConcurrentExecutions,
            executionRetentionDays,
            failureWorktreeRetentionDays);
    }

    public static Project Restore(
        Guid id,
        string displayName,
        string repository,
        bool enabled,
        string defaultBranch,
        int issueScanIntervalMinutes,
        int maxConcurrentExecutions,
        int executionRetentionDays,
        int failureWorktreeRetentionDays)
    {
        Validate(
            id,
            displayName,
            repository,
            defaultBranch,
            issueScanIntervalMinutes,
            maxConcurrentExecutions,
            executionRetentionDays,
            failureWorktreeRetentionDays);

        return new Project(
            id,
            displayName.Trim(),
            repository.Trim(),
            enabled,
            defaultBranch.Trim(),
            issueScanIntervalMinutes,
            maxConcurrentExecutions,
            executionRetentionDays,
            failureWorktreeRetentionDays);
    }

    public Project Update(
        string displayName,
        string repository,
        bool enabled,
        string defaultBranch,
        int issueScanIntervalMinutes,
        int maxConcurrentExecutions,
        int executionRetentionDays,
        int failureWorktreeRetentionDays)
    {
        return Restore(
            Id,
            displayName,
            repository,
            enabled,
            defaultBranch,
            issueScanIntervalMinutes,
            maxConcurrentExecutions,
            executionRetentionDays,
            failureWorktreeRetentionDays);
    }

    private static void Validate(
        Guid id,
        string displayName,
        string repository,
        string defaultBranch,
        int issueScanIntervalMinutes,
        int maxConcurrentExecutions,
        int executionRetentionDays,
        int failureWorktreeRetentionDays)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("プロジェクトIDは必須です。", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 100)
        {
            throw new ArgumentException("表示名は1文字以上100文字以下で指定してください。", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(repository) || !RepositoryPattern.IsMatch(repository.Trim()))
        {
            throw new ArgumentException("GitHubリポジトリは owner/repository 形式で指定してください。", nameof(repository));
        }

        if (string.IsNullOrWhiteSpace(defaultBranch) || defaultBranch.Trim().Length > 255)
        {
            throw new ArgumentException("既定ブランチは1文字以上255文字以下で指定してください。", nameof(defaultBranch));
        }

        if (issueScanIntervalMinutes is < 1 or > 1_440)
        {
            throw new ArgumentOutOfRangeException(nameof(issueScanIntervalMinutes), "Issue確認間隔は1分以上1440分以下で指定してください。");
        }

        if (maxConcurrentExecutions is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentExecutions), "最大同時実行数は1以上32以下で指定してください。");
        }

        if (executionRetentionDays is < 1 or > 3_650)
        {
            throw new ArgumentOutOfRangeException(nameof(executionRetentionDays), "実行履歴の保存日数は1日以上3650日以下で指定してください。");
        }

        if (failureWorktreeRetentionDays is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(failureWorktreeRetentionDays), "失敗時作業ツリーの保存日数は0日以上365日以下で指定してください。");
        }
    }
}
