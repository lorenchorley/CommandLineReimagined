/// Command names: at the head of a line, after a pipe, `else`, `try` or `(` (stream A).
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module CommandCompletion =

    /// <summary>The commands the word could name.</summary>
    /// <remarks>
    /// Stream A gives each its description as the detail, offers the commands that take
    /// the pipe straight after one, and finds `rm` for `delete`. Until then, the
    /// lexical rules.
    /// </remarks>
    let suggest (request: Request) (afterPipe: bool) : Async<Completion list> =
        ignore afterPipe
        async.Return(Lexical.answer request)
