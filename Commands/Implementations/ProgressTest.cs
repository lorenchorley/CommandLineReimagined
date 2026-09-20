using Terminal.Execution;

namespace Commands.Implementations
{
    /// <summary>
    /// A progress bar that runs for a while, to exercise async commands, live output,
    /// cancellation and undo.
    /// </summary>
    /// <remarks>
    /// No longer depends on the render loop: writing to <see cref="ICommandOutput"/> is
    /// enough, and the sink decides whether that means a redraw. That is what lets this
    /// run in the browser, where there is no loop to request.
    /// </remarks>
    public class ProgressTest : CommandActionAsync
    {
        private int _lastProcessedPercentage;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "progress",
                Description: "Runs a progress bar, to exercise async commands, cancellation and undo",
                KeyWords: "progress test bar",
                Parameters: new CommandParameter[]
                {
                    CommandParameter.Optional("steps", "How many steps to take; 100 by default"),
                    CommandParameter.Optional("delay", "Milliseconds between steps; 100 by default"),
                },
                CommandActionType: typeof(ProgressTest)
            );

        public override async Task<RuntimeValue> BeginInvoke(CommandInvocation invocation)
        {
            int steps = Number(invocation, "steps", 100);
            int delay = Number(invocation, "delay", 100);

            if (steps <= 0)
            {
                throw new ConsoleError("'steps' must be at least 1.");
            }

            var progressCounter = invocation.Output.NewLine().Write("progress", "0%");
            var progressBar = invocation.Output.NewLine().Write("progress", "");

            for (int i = 1; i <= steps; i++)
            {
                invocation.Cancellation.ThrowIfCancellationRequested();

                await Task.Delay(delay, invocation.Cancellation);

                _lastProcessedPercentage = (int)Math.Round(100.0 * i / steps);
                progressCounter.Text = $"{_lastProcessedPercentage}%";
                progressBar.Text = new string('=', _lastProcessedPercentage / 4) + ">";
            }

            return new NumberValue(_lastProcessedPercentage);
        }

        public override Task EndInvoke(CommandInvocation invocation)
        {
            invocation.Output.NewLine().Write("progress", "Progress test finished");
            return Task.CompletedTask;
        }

        public override Task FailedInvoke(CommandInvocation invocation, Task task)
        {
            string message = task.IsCanceled
                ? $"Cancelled at {_lastProcessedPercentage}%"
                : $"Progress test failed : {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";

            invocation.Output.NewLine().Write("progress", message);
            return Task.CompletedTask;
        }

        public override Task BeginInvokeUndo(CommandInvocation invocation)
        {
            invocation.Output.NewLine().Write("undo", "Progress test undone");
            return Task.CompletedTask;
        }

        private static int Number(CommandInvocation invocation, string name, int fallback)
        {
            if (!invocation.TryValue(name, out var value) || value is EmptyValue)
            {
                return fallback;
            }

            return value switch
            {
                NumberValue number => (int)number.Number,
                _ when int.TryParse(value.ToArgumentString(), out int parsed) => parsed,
                _ => throw new ConsoleError($"'{name}' must be a whole number, not '{value.ToDisplayString()}'."),
            };
        }
    }
}
