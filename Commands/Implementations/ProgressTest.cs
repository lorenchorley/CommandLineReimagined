using Controller;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class ProgressTest : CommandActionAsync
    {
        private readonly LoopController _loopController;

        private int _lastProcessedPercentage;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "progress",
                Description: "Runs a progress bar, to exercise async commands and undo",
                KeyWords: "progress test bar",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(ProgressTest)
            );

        public ProgressTest(LoopController loopController)
        {
            _loopController = loopController;
        }

        public override async Task<RuntimeValue> BeginInvoke(CommandInvocation invocation)
        {
            var line = invocation.Output.NewLine();
            var progressCounter = line.Write("progress", "0%");
            var progressBar = invocation.Output.NewLine().Write("progress", "");

            _loopController.RequestLoop();

            foreach (var i in Enumerable.Range(0, 100))
            {
                invocation.Cancellation.ThrowIfCancellationRequested();

                await Task.Delay(100, invocation.Cancellation);

                progressCounter.Text = $"{i}%";
                progressBar.Text = new string('=', i) + ">";
                _lastProcessedPercentage = i;
                _loopController.RequestLoop();
            }

            return new NumberValue(_lastProcessedPercentage);
        }

        public override Task EndInvoke(CommandInvocation invocation)
        {
            invocation.Output.NewLine().Write("progress", "Progress test finished");
            _loopController.RequestLoop();
            return Task.CompletedTask;
        }

        public override Task FailedInvoke(CommandInvocation invocation, Task task)
        {
            string message = task.IsCanceled
                ? $"Cancelled at {_lastProcessedPercentage}%"
                : $"Progress test failed : {task.Exception?.Message}";

            invocation.Output.NewLine().Write("progress", message);
            _loopController.RequestLoop();
            return Task.CompletedTask;
        }

        public override Task BeginInvokeUndo(CommandInvocation invocation)
        {
            invocation.Output.NewLine().Write("undo", "Progress test undone");
            _loopController.RequestLoop();
            return Task.CompletedTask;
        }
    }
}
