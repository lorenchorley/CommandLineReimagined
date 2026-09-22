using System.Text.Json.Serialization;
using CommandLineReimagined.Core;
using CommandLineReimagined.Web.Persistence;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Microsoft.JSInterop;

namespace CommandLineReimagined.WebClient;

/// <summary>
/// The log, kept in the browser's IndexedDB for this origin.
/// </summary>
/// <remarks>
/// Decision 0010's other half: once the filesystem is a fold over a log, persisting it
/// is persisting the log, and a reload is a replay. Nothing is uploaded; there is no
/// server to upload it to.
///
/// Storage can be unavailable (a private window) or go away mid-session (the user
/// clears site data). Neither is allowed to break the terminal, so every write that
/// fails falls back to memory and the log says so once. A session that has fallen back
/// still works for as long as the tab is open; it simply will not survive a reload,
/// and the page says `not persisted` so that is not a surprise.
///
/// Content is kept in memory as well as written, because a read after a write must not
/// depend on storage that may have just failed.
/// </remarks>
public sealed class IndexedDbLog : ILog
{
    private readonly IJSRuntime _js;
    private readonly List<Transaction> _transactions = new();
    private readonly Dictionary<string, string> _blobs = new();

    private IndexedDbLog(IJSRuntime js) => _js = js;

    /// <summary>What the JavaScript side answers with.</summary>
    /// <remarks>
    /// A result rather than an exception, because a failure here is expected and
    /// ordinary. Crossing the interop boundary as a thrown error would make a private
    /// window look like a defect.
    /// </remarks>
    private sealed record Result<T>(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("value")] T? Value,
        [property: JsonPropertyName("error")] string? Error);

    /// <summary>Whether writes are reaching storage.</summary>
    public bool IsPersistent { get; private set; }

    /// <summary>Why not, when they are not. Shown once, in the page's status line.</summary>
    public string? Unavailable { get; private set; }

    /// <summary>How many stored transactions could not be read.</summary>
    /// <remarks>
    /// A log written by a later build, or a row damaged by something outside this
    /// application. They are skipped rather than fatal: losing part of a history is
    /// bad, and refusing to open the terminal at all is worse, and `reset` is the way
    /// out either way.
    /// </remarks>
    public int Unreadable { get; private set; }

    /// <summary>
    /// Opens the store, or reports why it could not be opened.
    /// </summary>
    /// <remarks>
    /// Asked once, before anything is written, so the page can say `not persisted`
    /// before the user has typed rather than after their first write went nowhere.
    /// </remarks>
    public static async Task<IndexedDbLog> OpenAsync(IJSRuntime js)
    {
        var log = new IndexedDbLog(js);
        var result = await log.CallAsync<bool>("available");

        log.IsPersistent = result.Ok;
        log.Unavailable = result.Ok ? null : result.Error;

        return log;
    }

    private async Task<Result<T>> CallAsync<T>(string function, params object?[] arguments)
    {
        try
        {
            return await _js.InvokeAsync<Result<T>>($"clrStore.{function}", arguments)
                ?? new Result<T>(false, default, "The store did not answer.");
        }
        catch (Exception exception)
        {
            // The module may be missing entirely, or the page may be unloading. Either
            // way this is the same as storage being unavailable.
            return new Result<T>(false, default, exception.Message);
        }
    }

    /// Records that storage has stopped working, keeping the first reason rather than
    /// the latest: the first one is the one that explains the rest.
    private void FellBack(string? reason)
    {
        if (!IsPersistent) return;

        IsPersistent = false;
        Unavailable = reason ?? "Storage stopped responding.";
    }

    async Task<FSharpList<Transaction>> ReadAllAsync()
    {
        _transactions.Clear();
        Unreadable = 0;

        if (!IsPersistent)
        {
            return ListModule.Empty<Transaction>();
        }

        var result = await CallAsync<string[]>("readAllTransactions");

        if (!result.Ok)
        {
            FellBack(result.Error);
            return ListModule.Empty<Transaction>();
        }

        foreach (string json in result.Value ?? Array.Empty<string>())
        {
            try
            {
                _transactions.Add(LogFormat.Read(json));
            }
            catch (Exception)
            {
                Unreadable++;
            }
        }

        return ListModule.OfSeq(_transactions);
    }

    // The interface is F#-async. Each member wraps a task, which is what
    // Async.AwaitTask is for, and never blocks: WebAssembly has no thread to block on.

    FSharpAsync<Unit> ILog.Append(Transaction transaction) =>
        FSharpAsync.AwaitTask(AppendAsync(transaction));

    private async Task AppendAsync(Transaction transaction)
    {
        _transactions.Add(transaction);

        if (!IsPersistent) return;

        var result = await CallAsync<bool>("appendTransaction", transaction.Seq, LogFormat.Write(transaction));

        if (!result.Ok) FellBack(result.Error);
    }

    FSharpAsync<FSharpList<Transaction>> ILog.ReadAll() => FSharpAsync.AwaitTask(ReadAllAsync());

    FSharpAsync<string> ILog.PutBlob(string content) => FSharpAsync.AwaitTask(PutBlobAsync(content));

    private async Task<string> PutBlobAsync(string content)
    {
        string hash = HashModule.ofText(content);
        _blobs[hash] = content;

        if (IsPersistent)
        {
            var result = await CallAsync<bool>("putBlob", hash, content);
            if (!result.Ok) FellBack(result.Error);
        }

        return hash;
    }

    FSharpAsync<FSharpOption<string>> ILog.GetBlob(string hash) =>
        FSharpAsync.AwaitTask(GetBlobAsync(hash));

    private async Task<FSharpOption<string>> GetBlobAsync(string hash)
    {
        if (_blobs.TryGetValue(hash, out string? cached))
        {
            return FSharpOption<string>.Some(cached);
        }

        if (!IsPersistent) return FSharpOption<string>.None;

        var result = await CallAsync<string>("getBlob", hash);

        if (!result.Ok)
        {
            FellBack(result.Error);
            return FSharpOption<string>.None;
        }

        if (result.Value is null) return FSharpOption<string>.None;

        _blobs[hash] = result.Value;
        return FSharpOption<string>.Some(result.Value);
    }

    FSharpAsync<Unit> ILog.Clear() => FSharpAsync.AwaitTask(ClearAsync());

    private async Task ClearAsync()
    {
        _transactions.Clear();
        _blobs.Clear();

        if (!IsPersistent) return;

        var result = await CallAsync<bool>("clear");

        if (!result.Ok) FellBack(result.Error);
    }
}
