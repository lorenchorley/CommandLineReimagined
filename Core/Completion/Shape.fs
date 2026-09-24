/// What flows into a stage (decision 0031).
///
/// `ls | sort ` should offer the columns of what `ls` answers, and `ls documents |
/// where $row.kind eq ` the values in its `kind` column. Both need to know what the
/// stages before the cursor produce, and the only sure way to know is to run them.
namespace CommandLineReimagined.Core

open System
open System.Collections.Generic
open System.Diagnostics
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks

/// <summary>The columns flowing into a stage, and the rows when the upstream was run.</summary>
/// <remarks>
/// A shape is its columns and its rows, and two shapes with the same are equal: `Value`
/// is carried beside them, not compared, for a provider that reads more than a table,
/// such as a selector's element names in a tree (decision 0049).
/// </remarks>
[<CustomEquality; NoComparison>]
type Shape =
    { Columns: (string * ColumnType) list
      /// Present when the upstream was run and answered a table: each row by column name.
      Rows: Map<string, Value> list option
      /// What the upstream answered, whatever it was, when it was run or read.
      Value: Value option }

    override this.Equals(other: obj) =
        match other with
        | :? Shape as shape -> this.Columns = shape.Columns && this.Rows = shape.Rows
        | _ -> false

    override this.GetHashCode() = hash (this.Columns, this.Rows)

/// <summary>Shapes already learned, by upstream text and store sequence number.</summary>
/// <remarks>
/// So that typing inside the last stage does not re-run the stages before it: every
/// keystroke in `ls documents | where $row.ki` asks about the same `ls documents`, and
/// nothing it could answer changes until the store does. The session clears it on
/// every commit (`StoreChanged`); it also forgets on its own whatever was learned at
/// another sequence number, and an answer that arrives after a clear is dropped rather
/// than stored, so a run that started before a commit cannot come back to haunt the
/// line after it.
///
/// Locked, because on the desktop a late answer can land on a timer's thread while the
/// next request reads it. In the browser there is one thread and the lock is free.
/// </remarks>
[<Sealed>]
type ShapeCache() =
    let gate = obj ()
    let entries = Dictionary<string, Shape>(StringComparer.Ordinal)
    let mutable sequence = -1L
    let mutable generation = 0L

    /// More than anyone types between two commits; past it, start again.
    static member Limit = 64

    /// How many shapes it holds.
    member _.Count = lock gate (fun () -> entries.Count)

    /// Forgets everything, and refuses any answer to a question asked before now.
    member _.Clear() =
        lock gate (fun () ->
            entries.Clear()
            generation <- generation + 1L)

    /// Counts the clears, so an answer can say which state of the cache it was asked in.
    member _.Generation = lock gate (fun () -> generation)

    member _.TryFind(at: int64, upstream: string) : Shape option =
        lock gate (fun () ->
            match entries.TryGetValue upstream with
            | true, shape when at = sequence -> Some shape
            | _ -> None)

    /// Keeps a shape, unless the cache was cleared or the store moved on since it was asked.
    member _.Add(askedIn: int64, at: int64, upstream: string, shape: Shape) =
        lock gate (fun () ->
            if askedIn = generation && at >= sequence then
                if at <> sequence || entries.Count >= ShapeCache.Limit then
                    entries.Clear()
                    sequence <- at

                entries[upstream] <- shape)

/// <summary>What `Shape` may use to learn what flows in.</summary>
/// <remarks>
/// Built by the session for each request, so that completion never reaches the store
/// itself: it can read the projection and ask for a line to be previewed, and nothing
/// else.
/// </remarks>
type ShapeSource =
    { Projection: Projection
      /// <summary>Runs a line that only reads, and answers its value.</summary>
      /// <remarks>
      /// Nothing is committed, nothing reaches the history or the screen, and a line
      /// that would write is refused: `None`, as for a line that fails.
      /// </remarks>
      Preview: string -> CancellationToken -> Async<Value option>
      /// Cancelled when a newer keystroke asks again.
      Cancel: CancellationToken
      /// <summary>What earlier requests learned, keyed by `Projection.Applied`.</summary>
      /// <remarks>The session's, so that it outlives one request and a commit clears it.</remarks>
      Cache: ShapeCache }

[<RequireQualifiedAccess>]
module Shape =

    /// How long completion waits for the upstream before answering without it.
    let budget = TimeSpan.FromMilliseconds 150.0

    /// No columns at all: what flows in is not a table and cannot be read as one.
    let none = { Columns = []; Rows = None; Value = None }

    /// A table's columns, and its rows by column name.
    let ofTable (table: Table) : Shape =
        let names = Table.names table

        { Columns = table.Columns |> List.map (fun column -> column.Name, column.Type)
          Rows =
            table.Rows
            |> List.map (fun row -> names |> List.map (fun name -> name, Table.cell table name row) |> Map.ofList)
            |> Some
          Value = Some(Value.Table table) }

    /// <summary>The shape of a value that flows into a stage.</summary>
    /// <remarks>
    /// A table is itself. A tag or a list of tags goes through the coercion every table
    /// function applies, `table`'s included (decision 0009), so a stage is offered the
    /// columns it would actually be given. Anything else, and a tag that is not
    /// table-shaped, has no columns.
    /// </remarks>
    let ofValue (value: Value) : Shape =
        let shape =
            match value with
            | Value.Table table -> ofTable table
            | Value.Object _
            | Value.Component _
            | Value.List _ ->
                match Table.ofValue "table" value with
                | Ok table -> ofTable table
                | Error _ -> none
            | _ -> none

        { shape with Value = Some value }

    /// <summary>The columns a listing of the current folder would have, with no rows.</summary>
    /// <remarks>
    /// The five a record always has and every attribute anything here carries: what
    /// completion answered before Phase 8, and the answer whenever the upstream cannot
    /// be run.
    /// </remarks>
    let ofListing (projection: Projection) : Shape =
        let table =
            Table.ofRecords (fun _ -> 0.0) (Files.inFolder projection projection.Location.Folder)

        { Columns = table.Columns |> List.map (fun column -> column.Name, column.Type)
          Rows = None
          Value = None }

    /// `$name` alone, or `$name | the rest`: a variable at the head of the upstream.
    let private headVariable =
        Regex(@"^\$([A-Za-z_][A-Za-z0-9_-]*)\s*(?:\|\s*(.*))?$", RegexOptions.Singleline ||| RegexOptions.CultureInvariant)

    /// <summary>Runs a line through the preview, under the budget, and remembers the answer.</summary>
    /// <remarks>
    /// The run is started on the calling thread and the budget is measured from then.
    /// A run that finishes without ever waiting on anything has its answer used, however
    /// long it took: nothing can interrupt it, in the browser least of all, where it and
    /// the timer share one thread, and once the time is spent its answer is the better
    /// of the two. A run that does wait is answered for at the first wait that finds
    /// the budget spent, or when the timer fires while it waits, with the listing's
    /// shape.
    ///
    /// A missed budget does not stop the run. It goes on until it finishes, and its
    /// answer goes into the cache for the next keystroke, unless the next keystroke
    /// comes first and cancels it (`source.Cancel`). A cancelled run is not remembered:
    /// its failure says nothing about the line.
    /// </remarks>
    let private learn (source: ShapeSource) (upstream: string) (line: string) : Async<Shape> =
        let fallback = lazy (ofListing source.Projection)
        let at = source.Projection.Applied

        match source.Cache.TryFind(at, upstream) with
        | Some shape -> async.Return shape
        | None when source.Cancel.IsCancellationRequested -> async.Return fallback.Value
        | None ->
            let askedIn = source.Cache.Generation

            Async.FromContinuations(fun (answer, _, _) ->
                let settled = ref 0

                let settle (shape: Lazy<Shape>) =
                    if Interlocked.Exchange(&settled.contents, 1) = 0 then
                        answer shape.Value

                let clock = Stopwatch.StartNew()

                let finished (result: Value option) =
                    if source.Cancel.IsCancellationRequested then
                        settle fallback
                    else
                        let shape =
                            try
                                match result with
                                | Some value -> ofValue value
                                | None -> fallback.Value
                            with _ ->
                                fallback.Value

                        source.Cache.Add(askedIn, at, upstream, shape)
                        settle (lazy shape)

                let run =
                    async {
                        try
                            return! source.Preview line source.Cancel
                        with
                        | :? OperationCanceledException -> return None
                        | _ -> return None
                    }

                Async.StartWithContinuations(
                    run,
                    finished,
                    (fun _ -> settle fallback),
                    (fun _ -> settle fallback),
                    source.Cancel
                )

                // Here only when the run is waiting on something, or finished already.
                if settled.Value = 0 then
                    let left = budget - clock.Elapsed

                    if left <= TimeSpan.Zero then
                        settle fallback
                    else
                        Task.Delay(left).ContinueWith(fun (_: Task) -> settle fallback) |> ignore)

    /// <summary>What flows into a stage.</summary>
    /// <remarks>
    /// Nothing, at the head of a pipeline, where the stage lists the current folder
    /// unless it is given something else: the listing's columns. A lone variable is read
    /// from the projection, and nothing runs. Otherwise the upstream runs through
    /// `source.Preview`, which refuses a line that would write; a variable at its head
    /// is handed on by `echo`, so the stages after it run on its value. A refusal, a
    /// fault, a missed budget and a cancellation all answer the listing's columns,
    /// never an error.
    /// </remarks>
    let ofUpstream (source: ShapeSource) (stage: Stage) : Async<Shape> =
        match stage.Upstream |> Option.map (fun text -> text.Trim()) with
        | None
        | Some "" -> async.Return(ofListing source.Projection)
        | Some upstream ->
            let head = headVariable.Match upstream

            if not head.Success then
                learn source upstream upstream
            else
                let name = head.Groups[1].Value
                let rest = head.Groups[2]

                match Map.tryFind name source.Projection.Variables with
                // The line would fail on the name; nothing to run.
                | None -> async.Return(ofListing source.Projection)
                | Some value when not rest.Success -> async.Return(ofValue value)
                | Some _ when String.IsNullOrWhiteSpace rest.Value -> async.Return(ofListing source.Projection)
                | Some _ -> learn source upstream (sprintf "echo $%s | %s" name rest.Value)
