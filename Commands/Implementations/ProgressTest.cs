using CommandLine.Modules;
using Rendering;
using System.Threading;

namespace Commands.Implementations
{
    public class ProgressTest : CommandActionAsync
    {
        private readonly RenderLoop _renderLoop;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "progress",
                Description: "",
                KeyWords: "progress test",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(ProgressTest)
            );

        public ProgressTest(RenderLoop renderLoop)
        {
            _renderLoop = renderLoop;
        }

        public override async Task BeginInvoke(CommandParameterValue[] args, CliBlock scope, CancellationToken cancellationToken)
        {
            var progressCounter = scope.NewLine().LinkNewTextBlock("Failed", "0%");
            var progressBar = scope.NewLine().LinkNewTextBlock("Failed", "");

            _renderLoop.RefreshOnce();

            foreach (var i in Enumerable.Range(0, 100))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await Task.Delay(100);
                progressCounter.Text = $"{i}%";
                progressBar.Text = new string('=', i) + ">";
                _renderLoop.RefreshOnce();
            }
        }

        public override async Task EndInvoke(CommandParameterValue[] args, CliBlock scope)
        {
            var status = CancellationTokenSource.Token.IsCancellationRequested ? "unsuccessfully" : "successfully";
            scope.NewLine().LinkNewTextBlock("Failed", $"Progress test ended {status}");
            _renderLoop.RefreshOnce();
        }

        public override async Task FailedInvoke(CommandParameterValue[] args, CliBlock scope, Task task)
        {
            scope.NewLine().LinkNewTextBlock("Failed", "Progress test failed");
            _renderLoop.RefreshOnce();
        }

        public override async Task BeginInvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            scope.NewLine().LinkNewTextBlock("Failed", "Progress test undo !");

        }

    }
}
