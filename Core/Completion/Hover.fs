/// What a tapped token is (stream G).
namespace CommandLineReimagined.Core

/// What the page shows for a token.
type Hover =
    { /// `variable`, `command`, `member`, `operator` …, as the page colours it.
      Kind: string
      /// The token as written.
      Text: string
      /// The line about it: a variable's summary, a column's type, what an operator compares.
      Detail: string option
      /// A command's signature, for a command name.
      Signature: Signature option }

[<RequireQualifiedAccess>]
module Hover =

    /// <summary>What the token at the request's cursor is.</summary>
    /// <remarks>
    /// Answered from `Context` and `Summary` by stream G. `None` until then, and for a
    /// token there is nothing to say about.
    /// </remarks>
    let describe (request: Request) : Async<Hover option> =
        ignore request
        async.Return None
