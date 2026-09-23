/// What a completion is, and what every provider is given (Phase 8).
namespace CommandLineReimagined.Core

open System.Threading

/// <summary>One thing the word could become.</summary>
/// <remarks>
/// Replace the text from `Start` to `End` with `Text`. `End` is the end of the word,
/// not the end of the line, so completing in the middle of a line keeps the rest.
/// </remarks>
type Completion =
    { /// `command`, `keyword`, `variable`, `member`, `operator`, `file`, `folder`,
      /// `view`, and from Phase 8 `flag`, `column` and `value`. The page colours by it.
      Kind: string
      Text: string
      Start: int
      End: int
      /// A line about it: `table · 4 rows`, a command's description, a column's type.
      Detail: string option }

/// <summary>A command's parameters, with the one being written marked.</summary>
/// <remarks>`sort <column> [desc] [table]` with `column` active.</remarks>
type Signature =
    { Command: string
      Description: string
      /// Name, whether it is optional, and what it is for.
      Parameters: (string * bool * string) list
      /// The index in `Parameters` of the one the word would bind to.
      Active: int option }

type CompletionResult =
    { Items: Completion list
      Signature: Signature option }

/// Everything a provider is given.
type Request =
    { Specs: CommandSpec list
      Projection: Projection
      Context: Context
      /// What flows into a stage. `Shape.ofUpstream`, bound to the session's source.
      Shapes: Stage -> Async<Shape>
      Cancel: CancellationToken }

[<RequireQualifiedAccess>]
module Request =

    /// A completion that replaces the word under the cursor.
    let item (request: Request) (kind: string) (text: string) =
        { Kind = kind
          Text = text
          Start = request.Context.Word.Start
          End = request.Context.Word.End
          Detail = None }

    let withDetail (detail: string) (completion: Completion) = { completion with Detail = Some detail }
