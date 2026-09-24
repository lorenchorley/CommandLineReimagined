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

    /// Whether what a command takes from the pipe is a path or a place: a name, which a
    /// table has none of. `ls | read`, `ls | first | rm` and `ls | in` are all faults.
    let takesAPathFromThePipe (spec: CommandSpec) =
        spec.Parameters
        |> List.filter (fun parameter -> parameter.AcceptsPipe)
        |> List.forall (fun parameter -> parameter.Takes = Takes.Path || parameter.Takes = Takes.Place)

    /// <summary>The commands among `specs` the word could name, best first.</summary>
    /// <remarks>
    /// Three ways to match, ranked. The name starts with the word, which is completing
    /// it. One of the command's keywords starts with the word, which is finding it by
    /// what it does: `delete` finds `rm`, and the detail says why. The name is a slip or
    /// two away (`Nearest`), which is correcting it: `lss` finds `ls`. Keywords and slips
    /// need three letters: two start too many keywords (`se` would find `where` by
    /// `select`), and every two-letter word is one slip from half the short commands.
    /// A shorter word that is a whole keyword is the exception, because it is not a
    /// guess: `cd` and `up` are the old names of `in` and `out` (decision 0037).
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

        let matchesKeyword (keyword: string) =
            if searching then
                startsWith word keyword
            else
                word <> "" && String.Equals(word, keyword, StringComparison.OrdinalIgnoreCase)

        let byKeyword =
            specs
            |> List.filter (unmatched prefixed)
            |> List.choose (fun spec ->
                spec.Keywords
                |> List.tryFind matchesKeyword
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
    /// offers what could be done with a listing, not `mkdir`, which would ignore it.
    /// When the stages before it were run and answered a table, the commands that take
    /// a path from the pipe go too, because a table is not a name: `ls | ` does not
    /// offer `read` or `rm`, and `echo readme.txt | ` still offers `read`. A line that
    /// could not be run keeps them, since nothing says what flows in. The page's own
    /// words and `try` are offered where a command is written, as they were; the page's
    /// words not after a pipe, since `clear` takes nothing from one. An empty line is
    /// `Place.Blank`, which the dispatcher answers with nothing.
    /// </remarks>
    let suggest (request: Request) (afterPipe: bool) (upstream: string option) : Async<Completion list> =
        async {
            let word = request.Context.Word.Prefix

            let! aTable =
                match upstream with
                | Some _ when afterPipe ->
                    async {
                        let! shape =
                            request.Shapes
                                { Spec = None
                                  Name = ""
                                  Index = 1
                                  Upstream = upstream
                                  Written = [] }

                        return shape.Rows.IsSome
                    }
                | _ -> async.Return false

            let specs =
                if afterPipe then
                    request.Specs
                    |> List.filter takesThePipe
                    |> List.filter (fun spec -> not (aTable && takesAPathFromThePipe spec))
                else
                    request.Specs

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

            return among request specs @ pageWords @ keywords
        }
