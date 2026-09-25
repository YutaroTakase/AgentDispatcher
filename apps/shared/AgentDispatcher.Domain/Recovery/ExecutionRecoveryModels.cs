namespace AgentDispatcher.Domain.Recovery;

public sealed record RecoveryResult(
    int PreparingFailed,
    int RunningFailed,
    int RunningStillActive,
    IReadOnlyList<string> Errors);

public interface IExecutionRecovery
{
    Task<RecoveryResult> ReconcileAsync(
        CancellationToken cancellationToken = default);
}
