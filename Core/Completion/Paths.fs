/// Files, folders and views, by the path written so far (stream D).
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module PathCompletion =

    /// <summary>The records a path could name.</summary>
    /// <remarks>
    /// `places` keeps to folders and views, for `cd`. Stream D moves the path rules out
    /// of `Lexical` into here and adds quoted words: `cat "doc` offers `"documents/"`.
    /// Until then, the lexical rules.
    /// </remarks>
    let suggest (request: Request) (places: bool) : Completion list =
        ignore places
        Lexical.answer request
