/// Command names: at the head of a line, after a pipe, `else`, `try` or `(` (stream A).
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module CommandCompletion =

    let private startsWith (prefix: string) (candidate: string) =
        candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)

    /// Whether a command can stand after a pipe: one of its parameters takes the value.
    let takesThePipe (spec: CommandSpec) =
        spec.Parameters |> List.exists (fun parameter -> parameter.AcceptsPipe)

    /// <summary>The commands among `specs` the word could name, best first.</summary>
    /// <remarks>
    /// Three ways to match, ranked. The name starts with the word, which is completing
    /// it. One of the command's keywords starts with the word, which is finding it by
    /// what it does: `delete` finds `rm`, and the detail says why. The name is a slip or
    /// two away (`Nearest`), which is correcting it: `lss` finds `ls`. Keywords and slips
    /// need three letters: two start too many keywords (`se` would find `where` by
    /// `select`), and every two-letter word is one slip from half the short commands.
    ///
    /// Each chip carries the command's description, or for a keyword match the keyword
    /// it matched, so the chip says what it is before it is tapped.
    /// </remarks>
    let among (request: Request) (specs: CommandSpec list) : Completion list =
        let word = request.Context.Word.Prefix

        let described (spec: CommandSpec) =
            Request.item request "command" spec.Name |> Request.withDetail spec.Description

        let prefixed = specs |> List.filter (fun spec -> startsWith word spec.Name)

        let unmatched (found: CommandSpec list) (spec: CommandSpec) =
            not (found |> List.exists (fun other -> other.Name = spec.Name))

        let searching = word.Length >= 3

        let byKeyword =
            if not searching then
                []
            else
                specs
                |> List.filter (unmatched prefixed)
                |> List.choose (fun spec ->
                    spec.Keywords
                    |> List.tryFind (startsWith word)
                    |> Option.map (fun keyword -> spec, keyword))

        let near =
            if not searching then
                []
            else
                let found = prefixed @ (byKeyword |> List.map fst)
                let candidates = specs |> List.filter (unmatched found)

                Nearest.names (candidates |> List.map (fun spec -> spec.Name)) word
                |> List.choose (fun name -> candidates |> List.tryFind (fun spec -> spec.Name = name))

        (prefixed |> List.map described)
        @ (byKeyword
           |> List.map (fun (spec, keyword) ->
               Request.item request "command" spec.Name
               |> Request.withDetail (sprintf "%s · matches \"%s\"" spec.Name keyword)))
        @ (near |> List.map described)

    /// <summary>Every command the word could name, for a parameter that takes one.</summary>
    /// <remarks>
    /// `help `'s argument is a command's name (`Takes.CommandName`), and this is what
    /// it offers: the commands alone, without the page's words or `try`, which are not
    /// commands `help` could describe.
    /// </remarks>
    let names (request: Request) : Completion list = among request request.Specs

    /// <summary>The commands the word could name, where a stage starts.</summary>
    /// <remarks>
    /// Straight after a pipe, only the commands that take the pipe's value: `ls | `
    /// offers what could be done with a listing, not `mkdir`, which would ignore it. The
    /// page's own words and `try` are offered where a command is written, as they were;
    /// the page's words not after a pipe, since `clear` takes nothing from one. An empty
    /// line is `Place.Blank`, which the dispatcher answers with nothing.
    /// </remarks>
    let suggest (request: Request) (afterPipe: bool) : Async<Completion list> =
        let word = request.Context.Word.Prefix
        let specs = if afterPipe then request.Specs |> List.filter takesThePipe else request.Specs

        let pageWords =
            if afterPipe then
                []
            else
                Lexical.pageWords
                |> List.filter (startsWith word)
                |> List.map (Request.item request "command")

        let keywords =
            Lexical.stageKeywords
            |> List.filter (startsWith word)
            |> List.map (Request.item request "keyword")

        async.Return(among request specs @ pageWords @ keywords)
