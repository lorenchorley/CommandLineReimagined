/// The store: the log, the projection folded from it, and undo.
///
/// The only mutable thing in the core. Commands read `Current` and return events; the
/// evaluator commits. Nothing else appends.
namespace CommandLineReimagined.Core

open System

/// A transaction paired with whether its effect is currently in the projection.
type HistoryEntry = { Transaction: Transaction; Undone: bool }

type Store(log: ILog, clock: unit -> DateTimeOffset) =

    let mutable projection = Projection.empty
    let mutable transactions: Transaction list = []
    let mutable initialised = false
    let changed = Event<int64>()

    /// <summary>Whether a later transaction reverses this one and still stands.</summary>
    /// <remarks>
    /// Recursive, because a compensation can itself be compensated: `mkdir`, `undo`,
    /// `redo` leaves the `mkdir` in force, since the `undo` that reversed it has been
    /// reversed in turn. Termination is guaranteed by sequence numbers, which only
    /// ever increase.
    /// </remarks>
    let rec isCompensated (all: Transaction list) (target: Transaction) =
        all
        |> List.exists (fun candidate ->
            candidate.Compensates = Some target.Seq && not (isCompensated all candidate))

    let isCompensation (transaction: Transaction) = transaction.Compensates.IsSome

    /// The latest transaction that can be undone: one that did something itself, that
    /// someone typed, and whose doing has not already been taken back.
    let undoTarget () =
        transactions
        |> List.filter (fun t ->
            t.Undoable && not (isCompensation t) && not (isCompensated transactions t))
        |> List.sortByDescending (fun t -> t.Seq)
        |> List.tryHead

    /// The latest undo that has not itself been undone.
    let redoTarget () =
        transactions
        |> List.filter (fun t -> t.Undoable && isCompensation t && not (isCompensated transactions t))
        |> List.sortByDescending (fun t -> t.Seq)
        |> List.tryHead

    let nextSeq () =
        match transactions with
        | [] -> 1L
        | _ -> (transactions |> List.map (fun t -> t.Seq) |> List.max) + 1L

    /// <summary>Checks a batch of events against the state they will be applied to.</summary>
    /// <remarks>
    /// Against a running projection rather than the committed one, so a line that
    /// creates a folder and then puts a file in it validates the file against the
    /// folder it just made. A command has usually checked already; this is the store
    /// refusing to hold something inconsistent whatever the command believed, and it
    /// is the only place a `Conflict` can be raised after the fact.
    /// </remarks>
    let validate (start: Projection) (events: Event list) : Outcome<unit> =
        let rec loop (current: Projection) remaining =
            match remaining with
            | [] -> Ok()
            | event :: rest ->
                let problem =
                    match event with
                    | FileCreated record ->
                        let folder = Record.folder record
                        let name = Record.name record

                        if name = "" then
                            Some(Fault.create Invalid "A file must have a name.")
                        else
                            match Files.tryFindIn current folder name with
                            | Some existing when existing.Id <> record.Id ->
                                Some(Fault.nameAlreadyExists (Value.joinPath folder name))
                            | _ -> Option.None
                    | _ -> Option.None

                match problem with
                | Some fault -> Error fault
                | Option.None -> loop (Projection.apply current event) rest

        loop start events

    /// Appends a transaction that has already been validated, and says so.
    let append (transaction: Transaction) =
        async {
            do! log.Append transaction
            transactions <- transactions @ [ transaction ]
            projection <- { Projection.applyAll projection transaction.Events with Applied = transaction.Seq }
            changed.Trigger transaction.Seq
        }

    /// The clock is injectable so that tests can pin the timestamps `history` shows.
    new(log: ILog) = Store(log, fun () -> DateTimeOffset.UtcNow)

    /// Replays the log. Everything the session knows comes from here.
    member _.Initialize() =
        async {
            let! stored = log.ReadAll()
            transactions <- stored |> List.sortBy (fun t -> t.Seq)

            projection <-
                transactions
                |> List.fold
                    (fun state transaction ->
                        { Projection.applyAll state transaction.Events with Applied = transaction.Seq })
                    Projection.empty

            initialised <- true
        }

    member _.IsInitialised = initialised

    member _.Current = projection

    member _.Transactions = transactions

    /// Raised after every append, carrying the new sequence number. Phase 4's live
    /// views listen to it.
    member _.Changed = changed.Publish

    /// <summary>Appends one line's events as one transaction (decision 0015).</summary>
    /// <remarks>
    /// An empty list commits nothing and returns `None`, so a read-only line such as
    /// `ls` leaves no trace and `undo` reaches past it to the last line that changed
    /// something.
    /// </remarks>
    member _.Commit (source: string) (events: Event list) : Async<Outcome<Transaction option>> =
        async {
            if List.isEmpty events then
                return Ok Option.None
            else
                match validate projection events with
                | Error fault -> return Error fault
                | Ok() ->
                    let transaction =
                        { Seq = nextSeq ()
                          At = clock ()
                          Source = source
                          Events = events
                          Compensates = Option.None
                          Undoable = true }

                    do! append transaction
                    return Ok(Some transaction)
        }

    /// <summary>
    /// Appends a transaction that is recorded and replayed but is nobody's to undo
    /// (decision 0018). The seeded filesystem is the only one today.
    /// </summary>
    member _.CommitSystem (source: string) (events: Event list) : Async<Outcome<Transaction option>> =
        async {
            if List.isEmpty events then
                return Ok Option.None
            else
                match validate projection events with
                | Error fault -> return Error fault
                | Ok() ->
                    let transaction =
                        { Seq = nextSeq ()
                          At = clock ()
                          Source = source
                          Events = events
                          Compensates = Option.None
                          Undoable = false }

                    do! append transaction
                    return Ok(Some transaction)
        }

    /// <summary>Reverses the most recent line that still stands.</summary>
    /// <remarks>
    /// By appending, never by removing: the log only grows, so the fact that something
    /// was undone is itself part of the history and can be undone in turn. Returns the
    /// transaction that was reversed, so the caller can name it, or `None` when there
    /// was nothing to undo — which is not a fault, because nothing went wrong.
    /// </remarks>
    member _.Undo() : Async<Outcome<Transaction option>> =
        async {
            match undoTarget () with
            | Option.None -> return Ok Option.None
            | Some target ->
                let compensation =
                    { Seq = nextSeq ()
                      At = clock ()
                      Source = target.Source
                      Events = Projection.invertAll target.Events
                      Compensates = Some target.Seq
                      Undoable = true }

                do! append compensation
                return Ok(Some target)
        }

    /// Reverses the most recent undo, by compensating it. Returns the transaction the
    /// undo had reversed, so the caller names the original line rather than the undo.
    member _.Redo() : Async<Outcome<Transaction option>> =
        async {
            match redoTarget () with
            | Option.None -> return Ok Option.None
            | Some target ->
                let original =
                    target.Compensates
                    |> Option.bind (fun seq -> transactions |> List.tryFind (fun t -> t.Seq = seq))

                let compensation =
                    { Seq = nextSeq ()
                      At = clock ()
                      Source = target.Source
                      Events = Projection.invertAll target.Events
                      Compensates = Some target.Seq
                      Undoable = true }

                do! append compensation
                return Ok(Some(defaultArg original target))
        }

    /// <summary>The log, oldest first, with whether each line still stands.</summary>
    /// <remarks>
    /// A compensation is never itself marked undone. It reads as a line in its own
    /// right — "you undid this" — and marking an undo as undone after a redo says
    /// nothing a reader can act on.
    /// </remarks>
    member _.History() : HistoryEntry list =
        transactions
        |> List.map (fun transaction ->
            { Transaction = transaction
              Undone = not (isCompensation transaction) && isCompensated transactions transaction })

    member _.PutBlob(content: string) = log.PutBlob content

    member _.GetBlob(hash: Hash) = log.GetBlob hash
