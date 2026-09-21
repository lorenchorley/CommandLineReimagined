using System.Net.Http;
using CommandLineReimagined.Core;
using Microsoft.FSharp.Core;

namespace Terminal.Execution;

/// <summary>
/// Builds the session the desktop shell runs on.
/// </summary>
/// <remarks>
/// The whole of the desktop's execution layer. What used to be a folder of command
/// implementations, a binder, an evaluator and a history is now this: a log, four
/// options, and the core.
///
/// The log is in memory, so the filesystem lives as long as the window does. Decision
/// 0012 leaves a projection onto the real disk out of scope, and the browser is the
/// same until Phase 2 persists its log; this is not a regression from what the desktop
/// had, it is the same model with the disk removed.
/// </remarks>
public static class DesktopSession
{
    private static readonly HttpClient HttpClient = new();

    public static Session Create(IApplicationLifetime lifetime)
    {
        var options = new SessionOptions(
            clock: FuncConvert.FromFunc<DateTimeOffset>(() => DateTimeOffset.UtcNow),
            newId: FuncConvert.FromFunc(() => Guid.NewGuid().ToString().ToLowerInvariant()),
            httpClient: FuncConvert.FromFunc(() => HttpClient),
            exit: FuncConvert.FromAction(lifetime.Shutdown));

        return new Session(
            new InMemoryLog(),
            options,
            SeedModule.standard(options.NewId, options.Clock));
    }
}
