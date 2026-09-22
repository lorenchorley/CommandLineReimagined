/// The log: what happened, in order, and never edited.
///
/// Decision 0010. A command describes what it did as a list of events; the store
/// applies them and appends them. Everything else — the filesystem, the variables,
/// where you are — is a fold over this.
namespace CommandLineReimagined.Core

open System
open System.Security.Cryptography
open System.Text

/// The SHA-256 of a piece of content, lower-case hex. Content is addressed by hash so
/// that a write's before-image costs nothing to keep: the old text is already in the
/// blob store and the event carries only the two hashes.
type Hash = string

/// <summary>A file: a set of typed attributes and optionally some content.</summary>
/// <remarks>
/// Decision 0013. There is no separate notion of a directory; a folder is a record
/// whose `kind` attribute is `folder` and which has no content (decision 0016).
/// </remarks>
type FileRecord =
    { Id: FileId
      Attributes: Map<string, Value>
      Content: Hash option }

/// Where the session is. `View` is a saved query, and stays `None` until Phase 4.
type Location = { Folder: string; View: Expr option }

/// <summary>Something that happened.</summary>
/// <remarks>
/// Every case carries both sides of the change, so inverting one needs nothing but the
/// event itself — no lookup, no projection, no ordering assumption. That is what makes
/// undo total and what lets a compensating transaction be built from a transaction
/// alone.
/// </remarks>
type Event =
    | FileCreated of FileRecord
    | FileDeleted of FileRecord
    | AttributesChanged of id: FileId * before: Map<string, Value> * after: Map<string, Value>
    | ContentChanged of id: FileId * before: Hash option * after: Hash option
    | VariableChanged of name: string * before: Value option * after: Value option
    | LocationChanged of before: Location * after: Location

/// <summary>One command line's worth of change (decision 0015).</summary>
/// <remarks>
/// `Compensates` names the transaction this one reverses, and is what makes undo and
/// redo the same operation seen from two sides: undo compensates a transaction, redo
/// compensates the compensation. The log only ever grows.
/// </remarks>
type Transaction =
    { Seq: int64
      At: DateTimeOffset
      /// The command line, as the user wrote it. This is what `history` shows and what
      /// `Undone: ...` names.
      Source: string
      Events: Event list
      Compensates: int64 option
      /// <summary>Whether this is a line someone typed (decision 0018).</summary>
      /// <remarks>
      /// The seeded filesystem is recorded and replayed like anything else, so that a
      /// store stays a pure fold of its log, but it is not the user's to take back:
      /// `undo` as the first thing in a fresh session used to empty the very files
      /// that are there to be looked at.
      /// </remarks>
      Undoable: bool }

/// <summary>Where transactions and content live.</summary>
/// <remarks>
/// Async throughout, because the browser's IndexedDB is (Phase 2) and WebAssembly is
/// single threaded, so there is nowhere to block. The in-memory implementation returns
/// completed computations and pays nothing for the shape.
/// </remarks>
type ILog =
    abstract Append: Transaction -> Async<unit>
    abstract ReadAll: unit -> Async<Transaction list>
    abstract PutBlob: string -> Async<Hash>
    abstract GetBlob: Hash -> Async<string option>

    /// <summary>Throws everything away.</summary>
    /// <remarks>
    /// The one operation that is not append-only, and the only way out of a log that
    /// cannot be read back. It exists for `reset`; nothing else may call it, which is
    /// why it is not reachable through the capability commands are given.
    /// </remarks>
    abstract Clear: unit -> Async<unit>

[<RequireQualifiedAccess>]
module Hash =

    let ofText (text: string) : Hash =
        let bytes = SHA256.HashData(Encoding.UTF8.GetBytes text)
        Convert.ToHexString(bytes).ToLowerInvariant()

/// The log the tests and the desktop shell use. Nothing survives the process.
type InMemoryLog() =
    let transactions = ResizeArray<Transaction>()
    let blobs = System.Collections.Generic.Dictionary<Hash, string>()

    member _.Count = transactions.Count

    interface ILog with
        member _.Append transaction =
            transactions.Add transaction
            async.Return()

        member _.ReadAll() = async.Return(List.ofSeq transactions)

        member _.PutBlob content =
            let hash = Hash.ofText content
            // Content-addressed, so storing the same text twice stores it once. That
            // is what makes keeping every version of every file affordable.
            blobs[hash] <- content
            async.Return hash

        member _.GetBlob hash =
            match blobs.TryGetValue hash with
            | true, content -> async.Return(Some content)
            | _ -> async.Return Option.None

        member _.Clear() =
            transactions.Clear()
            blobs.Clear()
            async.Return()
