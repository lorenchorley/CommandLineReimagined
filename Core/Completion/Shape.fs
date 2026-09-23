/// What flows into a stage (decision 0031).
///
/// `ls | sort ` should offer the columns of what `ls` answers, and `ls documents |
/// where $row.kind eq ` the values in its `kind` column. Both need to know what the
/// stages before the cursor produce, and the only sure way to know is to run them.
namespace CommandLineReimagined.Core

open System.Threading

/// The columns flowing into a stage, and the rows when the upstream was run.
type Shape =
    { Columns: (string * ColumnType) list
      /// Present when the upstream was run and answered a table: each row by column name.
      Rows: Map<string, Value> list option }

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
      Cancel: CancellationToken }

[<RequireQualifiedAccess>]
module Shape =

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
          Rows = None }

    /// <summary>What flows into a stage.</summary>
    /// <remarks>
    /// Stream F makes this run `stage.Upstream` through `source.Preview`, under a time
    /// budget and a cache. Until then it is the listing's columns, whatever the stage.
    /// </remarks>
    let ofUpstream (source: ShapeSource) (stage: Stage) : Async<Shape> =
        ignore stage
        async.Return(ofListing source.Projection)
