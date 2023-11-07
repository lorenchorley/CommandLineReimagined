﻿using CommandLine.Modules;
using Console.Components;
using Rendering;
using System.Threading;

namespace Commands.Implementations
{
    public class ProgressTest : CommandActionAsync
    {
        private readonly RenderLoop _renderLoop;
        private readonly Random _random = new();
        private int _lastProcessedPercentage = 0;

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

        private TextComponent? progressCounter;
        private TextComponent? progressBar;

        public override async Task BeginInvoke(CommandParameterValue[] args, CliBlock scope, CancellationToken cancellationToken)
        {
            progressCounter = scope.NewLine().LinkNewTextBlock("Failed", "0%");
            progressBar = scope.NewLine().LinkNewTextBlock("Failed", "");

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
                _lastProcessedPercentage = i;
                _renderLoop.RefreshOnce();

                if(_random.Next(0, 100) == 0)
                {
                    throw new Exception("Random exception !");
                }
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
            scope.NewLine().LinkNewTextBlock("Failed", $"Progress test task failed with status {task.Status} : {task.Exception?.Message}");
            _renderLoop.RefreshOnce();
        }

        public override async Task BeginInvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            var undoText = scope.NewLine().LinkNewTextBlock("Failed", "Progress test received undo command");

            await Task.Delay(500);
            undoText.Text += ", undoing";
            _renderLoop.RefreshOnce();

            foreach (var _ in Enumerable.Range(1, 3))
            {
                await Task.Delay(200);
                undoText.Text += ".";
                _renderLoop.RefreshOnce();
            }

            foreach (var i in Enumerable.Range(1, _lastProcessedPercentage))
            {
                await Task.Delay(30);
                int progress = _lastProcessedPercentage - i;
                progressCounter.Text = $"{progress}%";
                progressBar.Text = new string('=', progress) + "<";
                _renderLoop.RefreshOnce();
            }
        }

    }
}
