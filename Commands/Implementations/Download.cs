using Controller;
using System.Diagnostics;
using System.Net.Http;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class Download : CommandActionAsync
    {
        private readonly LoopController _loopController;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string DefaultUrl = @"https://releases.ubuntu.com/22.04.3/ubuntu-22.04.3-desktop-amd64.iso";

        private string? _fullPath;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "download",
                Description: "Download a file, with progress",
                KeyWords: "download file transfer api stream http ftp",
                Parameters: new CommandParameter[]
                {
                    // Was hard-coded to one URL and one output directory with a TODO.
                    CommandParameter.Optional("url", "What to download"),
                    CommandParameter.Optional("into", "Directory to download into"),
                },
                CommandActionType: typeof(Download)
            );

        public Download(LoopController loopController, IHttpClientFactory httpClientFactory)
        {
            _loopController = loopController;
            _httpClientFactory = httpClientFactory;
        }

        public override async Task<RuntimeValue> BeginInvoke(CommandInvocation invocation)
        {
            string url = Text(invocation, "url", DefaultUrl);
            string directory = Text(invocation, "into", Directory.GetCurrentDirectory());

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ConsoleError($"Not a valid URL : {url}");
            }

            if (!Directory.Exists(directory))
            {
                throw new ConsoleError($"Target directory does not exist : {directory}");
            }

            _fullPath = Path.Combine(directory, Path.GetFileName(uri.LocalPath));

            if (File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
            }

            var progressCounter = invocation.Output.NewLine().Write("Download", "0%");
            var progressBar = invocation.Output.NewLine().Write("Download", "");
            var speedCounter = invocation.Output.NewLine().Write("Download", "");

            _loopController.RequestLoop();

            await DownloadStreamToFile(uri, progressCounter, progressBar, speedCounter, invocation.Cancellation);

            return new PathValue(_fullPath, PathKind.File);
        }

        private async Task DownloadStreamToFile(
            Uri uri,
            IOutputText progressCounter,
            IOutputText progressBar,
            IOutputText speedCounter,
            CancellationToken cancellationToken)
        {
            using HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage transfer =
                await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (transfer.Content.Headers.ContentLength == null)
            {
                throw new ConsoleError("The server did not report a content length.");
            }

            long totalSizeBytes = (long)transfer.Content.Headers.ContentLength;

            var stream = await transfer.Content.ReadAsStreamAsync(cancellationToken);
            long bytesRemaining = totalSizeBytes;
            byte[] buffer = new byte[4096];

            using FileStream fileStream = new(_fullPath!, FileMode.Append, FileAccess.Write, FileShare.None, 4096, true);

            var stopwatch = Stopwatch.StartNew();
            int bytesDownloadedInLastSecond = 0;

            double[] speeds = new double[10];
            int speedIndex = 0;

            while (bytesRemaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int bytesRead = await ReadChunk(bytesRemaining, stream, fileStream, buffer, cancellationToken);

                // A zero-length read means the server closed early; without this the loop
                // spins forever on a truncated transfer.
                if (bytesRead == 0)
                {
                    throw new ConsoleError("The transfer ended before all bytes arrived.");
                }

                bytesRemaining -= bytesRead;

                var progress = (int)(((double)(totalSizeBytes - bytesRemaining) / totalSizeBytes) * 100);
                progressCounter.Text = $"{progress}%";
                progressBar.Text = new string('=', progress) + ">";

                bytesDownloadedInLastSecond += bytesRead;
                if (stopwatch.Elapsed.TotalSeconds > 0.25)
                {
                    stopwatch.Stop();
                    double speed = bytesDownloadedInLastSecond / stopwatch.Elapsed.TotalSeconds;
                    speeds[speedIndex++ % speeds.Length] = speed;
                    speedCounter.Text = $"{(speeds.Average() / 1024 / 1024):F2} MB/s";
                    stopwatch.Restart();
                    bytesDownloadedInLastSecond = 0;
                }

                _loopController.RequestLoop();
            }

            await fileStream.FlushAsync(cancellationToken);
        }

        private static async Task<int> ReadChunk(
            long bytesRemaining, Stream inStream, Stream outStream, byte[] buffer, CancellationToken cancellationToken)
        {
            int chunkSize = (int)Math.Min(bytesRemaining, buffer.Length);
            int bytesRead = await inStream.ReadAsync(buffer.AsMemory(0, chunkSize), cancellationToken);
            await outStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            return bytesRead;
        }

        public override Task EndInvoke(CommandInvocation invocation)
        {
            invocation.Output.NewLine().Write("Download", $"Downloaded to {_fullPath}");
            _loopController.RequestLoop();
            return Task.CompletedTask;
        }

        public override Task FailedInvoke(CommandInvocation invocation, Task task)
        {
            string message = task.IsCanceled
                ? "Cancelled"
                : $"Download failed : {task.Exception?.InnerException?.Message ?? task.Exception?.Message}";

            invocation.Output.NewLine().Write("Download", message);
            _loopController.RequestLoop();
            return Task.CompletedTask;
        }

        public override Task BeginInvokeUndo(CommandInvocation invocation)
        {
            if (_fullPath is not null && File.Exists(_fullPath))
            {
                File.Delete(_fullPath);
                invocation.Output.NewLine().Write("Undo", "Download deleted");
            }

            _loopController.RequestLoop();
            return Task.CompletedTask;
        }

        private static string Text(CommandInvocation invocation, string name, string fallback) =>
            invocation.TryValue(name, out var value) && value is not EmptyValue
                ? value.ToArgumentString()
                : fallback;
    }
}
