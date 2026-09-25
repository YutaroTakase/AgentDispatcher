using AgentDispatcher.Domain.Codex;
using AgentDispatcher.Domain.Executions;
using AgentDispatcher.Domain.Recovery;

namespace AgentDispatcher.Infrastructure.Recovery;

public sealed class ExecutionRecovery(
    IExecutionRepository executions,
    IExecutionQueryRepository queries,
    ICodexRunner codex) : IExecutionRecovery
{
    public async Task<RecoveryResult> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        var preparingFailed = 0;
        var runningFailed = 0;
        var runningStillActive = 0;
        var errors = new List<string>();

        var preparing = await queries.ListAsync(
            new ExecutionQuery(
                Status: ExecutionStatus.Preparing,
                Limit: 1000),
            cancellationToken);

        foreach (var execution in preparing)
        {
            try
            {
                await executions.TransitionAsync(
                    execution.Id,
                    ExecutionStatus.Failed,
                    new ExecutionTransitionData(
                        FailureSummary: "AgentDispatcherの再起動により準備処理が中断されました。",
                        Detail: "再起動後の復旧処理で中断済みと判定しました。"),
                    cancellationToken);
                preparingFailed++;
            }
            catch (Exception exception)
            {
                errors.Add($"実行 {execution.Id}: 準備中状態を復旧できません: {exception.Message}");
            }
        }

        var running = await queries.ListAsync(
            new ExecutionQuery(
                Status: ExecutionStatus.Running,
                Limit: 1000),
            cancellationToken);

        foreach (var execution in running)
        {
            try
            {
                if (await codex.IsRunningAsync(execution.Id, cancellationToken))
                {
                    runningStillActive++;
                    continue;
                }

                await executions.TransitionAsync(
                    execution.Id,
                    ExecutionStatus.Failed,
                    new ExecutionTransitionData(
                        FailureSummary: "Codex実行プロセスを確認できませんでした。",
                        Detail: "再起動後の復旧処理でCodexプロセスの終了または消失を検出しました。"),
                    cancellationToken);
                runningFailed++;
            }
            catch (Exception exception)
            {
                errors.Add($"実行 {execution.Id}: 実行中状態を復旧できません: {exception.Message}");
            }
        }

        return new RecoveryResult(
            preparingFailed,
            runningFailed,
            runningStillActive,
            errors);
    }
}
