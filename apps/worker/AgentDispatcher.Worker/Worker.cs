using AgentDispatcher.Domain.Dispatching;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Maintenance;
using AgentDispatcher.Domain.Projects;

namespace AgentDispatcher.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IProjectRepository projects,
    IProjectScanStateRepository scanStates,
    IDispatchCoordinator dispatcher,
    IRetentionCleanup cleanup) : BackgroundService
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AgentDispatcherの常駐処理を開始しました。");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanDueProjectsAsync(stoppingToken);
                await dispatcher.ProcessQueuedAsync(stoppingToken);
                await cleanup.RunIfDueAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "常駐処理で予期しないエラーが発生しました。");
            }

            try
            {
                await Task.Delay(LoopInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("AgentDispatcherの常駐処理を終了しました。");
    }

    private async Task ScanDueProjectsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await projects.ListAsync(cancellationToken);

        foreach (var project in items.Where(item => item.Enabled))
        {
            var lastScan = await scanStates.GetLastScanAsync(
                project.Id,
                cancellationToken);

            if (lastScan is { } previous &&
                now - previous < TimeSpan.FromMinutes(project.IssueScanIntervalMinutes))
            {
                continue;
            }

            var result = await dispatcher.ScanProjectAsync(
                project.Id,
                ExecutionTrigger.Scheduled,
                cancellationToken);

            await scanStates.SetLastScanAsync(
                project.Id,
                now,
                cancellationToken);

            foreach (var decision in result.Decisions)
            {
                logger.LogInformation(
                    "プロジェクト {ProjectId} Issue {IssueNumber}: {Decision} - {Detail}",
                    project.Id,
                    decision.IssueNumber,
                    decision.Kind,
                    decision.Detail);
            }
        }
    }
}
