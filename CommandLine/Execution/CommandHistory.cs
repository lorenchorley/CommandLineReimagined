using Commands;

namespace Terminal.Execution;

/// <summary>
/// The stack of executed commands, and the undo that walks back down it.
/// </summary>
/// <remarks>
/// Replaces CommandHistoryModule. Same behaviour, but it stores the
/// <see cref="CommandInvocation"/> so undo gets the arguments, scope and output the
/// command actually ran with, rather than a loose args/block tuple.
/// </remarks>
public sealed class CommandHistory
{
    private readonly Stack<(ICommandAction Action, CommandInvocation Invocation)> _executed = new();

    public int Count => _executed.Count;

    public void Register(ICommandAction action, CommandInvocation invocation) =>
        _executed.Push((action, invocation));

    public void UndoLast()
    {
        if (_executed.Count == 0)
        {
            return;
        }

        var (action, invocation) = _executed.Pop();

        switch (action)
        {
            case CommandActionSync sync:
                sync.InvokeUndo(invocation);
                Clear(invocation);
                break;

            case CommandActionAsync async:
            {
                // First undo cancels and leaves the output visible; a second one clears
                // it. Keeps a long-running command's output readable after cancelling.
                if (async.AlreadyCancelled)
                {
                    Clear(invocation);
                    return;
                }

                async.AlreadyCancelled = true;
                _executed.Push((async, invocation));

                if (async.CurrentTask is null)
                {
                    _ = Task.Run(() => async.BeginInvokeUndo(invocation));
                }
                else
                {
                    async.CancellationTokenSource?.Cancel();
                    _ = async.CurrentTask.ContinueWith(_ => async.BeginInvokeUndo(invocation));
                }

                break;
            }
        }
    }

    private static void Clear(CommandInvocation invocation)
    {
        if (invocation.Output is IClearableOutput clearable)
        {
            clearable.Clear();
        }
    }
}

/// <summary>Output that can withdraw everything it has written, for undo.</summary>
public interface IClearableOutput
{
    void Clear();
}
