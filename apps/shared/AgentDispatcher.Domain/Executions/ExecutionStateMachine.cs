namespace AgentDispatcher.Domain.Executions;

public static class ExecutionStateMachine
{
    public static void EnsureTransition(
        ExecutionStatus current,
        ExecutionStatus target)
    {
        if (current == target)
        {
            throw new InvalidOperationException(
                $"実行状態は既に {current} です。");
        }

        var allowed = current switch
        {
            ExecutionStatus.Queued =>
                target is ExecutionStatus.Preparing
                    or ExecutionStatus.Canceled
                    or ExecutionStatus.Failed,
            ExecutionStatus.Preparing =>
                target is ExecutionStatus.Running
                    or ExecutionStatus.Canceled
                    or ExecutionStatus.Failed,
            ExecutionStatus.Running =>
                target is ExecutionStatus.Succeeded
                    or ExecutionStatus.Failed
                    or ExecutionStatus.Canceled,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"実行状態を {current} から {target} へ変更できません。");
        }
    }

    public static bool IsActive(ExecutionStatus status) =>
        status is ExecutionStatus.Queued
            or ExecutionStatus.Preparing
            or ExecutionStatus.Running;

    public static bool IsTerminal(ExecutionStatus status) =>
        status is ExecutionStatus.Succeeded
            or ExecutionStatus.Failed
            or ExecutionStatus.Canceled;
}
