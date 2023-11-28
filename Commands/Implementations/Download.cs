using CommandLine.Modules;
using UIComponents.Components;
using Rendering;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using Controller;

namespace Commands.Implementations
{
    public class Download : CommandActionAsync
    {
        private readonly RenderLoop _renderLoop;
        private readonly LoopController _loopController;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Random _random = new();
        //private string _url = @"http://research.nhm.org/pdfs/10840/10840-002.pdf";
        private string _url = @"https://releases.ubuntu.com/22.04.3/ubuntu-22.04.3-desktop-amd64.iso";
        private const string _fileLocation = @"c:\Test\";
        private string _fullPath;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "download",
                Description: "",
                KeyWords: "download file transfer api stream http ftp",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(Download)
            );

        public Download(LoopController loopController, IHttpClientFactory httpClientFactory)
        {
            _loopController = loopController;
            _httpClientFactory = httpClientFactory;
        }

        private TextComponent? progressCounter;
        private TextComponent? progressBar;
        private TextComponent? speedCounter;

        public override async Task BeginInvoke(CommandParameterValue[] args, CliBlock scope, CancellationToken cancellationToken)
        {
            progressCounter = scope.NewLine().LinkNewTextBlock("Download", "0%");
            progressBar = scope.NewLine().LinkNewTextBlock("Download", "");
            speedCounter = scope.NewLine().LinkNewTextBlock("Download", "");

            _loopController.RequestLoop();

            // TODO Get from args
            var filename = Path.GetFileName(_url);
            _fullPath = Path.Combine(_fileLocation, filename);

            // TODO Copy to a temporay location so that undo can put it back if needed
            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
            }

            // TODO Get from args
            var uri = new Uri(_url);

            await DownloadStreamToFile(uri, scope, cancellationToken);
        }

        private async Task DownloadStreamToFile(Uri uri, CliBlock scope, CancellationToken cancellationToken)
        {
            using HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage transfer = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (transfer.Content.Headers.ContentLength == null)
            {
                throw new Exception("No content length given"); // TODO Custom exception so that it can be displayed correctly
            }

            long totalSizeBytes = (long)transfer.Content.Headers.ContentLength;

            var stream = await transfer.Content.ReadAsStreamAsync();
            long bytesRemaining = totalSizeBytes;
            byte[] buffer = new byte[4096];

            using FileStream fileStream = new(_fullPath, FileMode.Append, FileAccess.Write, FileShare.None, 4096, true);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int bytesDownloadedInLastSecond = 0;

            double[] speeds = new double[10];
            int speedIndex = 0;

            while (bytesRemaining > 0)
            {
                // Download chunk
                int bytesRead = await ReadChunk(bytesRemaining, stream, fileStream, buffer, cancellationToken);
                bytesRemaining -= bytesRead;


                // Update display
                var progress = (int)(((double)(totalSizeBytes - bytesRemaining) / (double)totalSizeBytes) * 100);
                progressCounter.Text = $"{progress}%";
                progressBar.Text = new string('=', progress) + ">";

                bytesDownloadedInLastSecond += bytesRead;
                if (stopwatch.Elapsed.TotalSeconds > 0.25)
                {
                    stopwatch.Stop();
                    double speed = (double)bytesDownloadedInLastSecond / stopwatch.Elapsed.TotalSeconds;
                    speeds[speedIndex++ % speeds.Length] = speed;
                    var averageSpeed = speeds.Average();
                    speedCounter.Text = $"{(averageSpeed / 1024 / 1024):F2} MB/s";
                    stopwatch.Restart();
                    bytesDownloadedInLastSecond = 0;
                }

                _loopController.RequestLoop();
            }

            await fileStream.FlushAsync(cancellationToken);
        }

        private async Task<int> ReadChunk(long bytesRemaining, Stream inStream, Stream outStream, byte[] buffer, CancellationToken cancellationToken)
        {
            int chunkSize = Math.Min((int)bytesRemaining, 1024);
            int bytesRead = await inStream.ReadAsync(buffer, 0, chunkSize, cancellationToken);
            await outStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            return bytesRead;
        }

        public override async Task EndInvoke(CommandParameterValue[] args, CliBlock scope)
        {
            var status = CancellationTokenSource.Token.IsCancellationRequested ? "unsuccessfully" : "successfully";
            scope.NewLine().LinkNewTextBlock("Download", $"Progress test ended {status}");
            _loopController.RequestLoop();
        }

        public override async Task FailedInvoke(CommandParameterValue[] args, CliBlock scope, Task task)
        {
            if (task.Status == TaskStatus.Canceled)
            {
                scope.NewLine().LinkNewTextBlock("Failed", $"Cancelled");
            }
            else
            {
                scope.NewLine().LinkNewTextBlock("Failed", $"Progress test task failed with status {task.Status} : {task.Exception?.Message}");
            }

            _loopController.RequestLoop();
        }

        public override async Task BeginInvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {
            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
                scope.NewLine().LinkNewTextBlock("Undo", "Download deleted");
            }

            _loopController.RequestLoop();
        }

    }
}
