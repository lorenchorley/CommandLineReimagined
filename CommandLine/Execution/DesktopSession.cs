using System.Net.Http;
using CommandLineReimagined.Core;

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
/// 0012 leaves a projection onto the real disk out of scope, and a file-backed log for
/// the desktop was an optional item the plan did not take up; the browser persists its
/// log in IndexedDB. This is the same model as the browser's with the storage removed.
/// </remarks>
public static class DesktopSession
{
    private static readonly HttpClient HttpClient = new();

    public static Session Create(IApplicationLifetime lifetime)
    {
        // Plain delegates; the core converts them. See the note in the browser adapter
        // on why FuncConvert is not used here.
        var options = SessionOptionsModule.ofDelegates(
            () => DateTimeOffset.UtcNow,
            () => Guid.NewGuid().ToString().ToLowerInvariant(),
            () => HttpClient,
            lifetime.Shutdown);

        return new Session(new InMemoryLog(), options, SessionOptionsModule.standardSeed(options));
    }
}
