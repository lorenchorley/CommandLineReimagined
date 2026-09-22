/// Failure as a value.
///
/// Decision 0006: no command throws, and no exception crosses a module boundary. A
/// command that cannot do what it was asked returns a fault, the evaluator stops the
/// line, and the session renders the message. Exceptions are for programmer bugs and
/// become a fault of kind `Internal` at the session boundary.
namespace CommandLineReimagined.Core

/// <summary>What sort of failure it is.</summary>
/// <remarks>
/// The kind is additional to the message rather than a replacement for it: every
/// message in the error reference is preserved word for word, because those are what
/// a user reads, and the kind is what a program matches on once `try` binds a fault
/// to a variable in Phase 5.
/// </remarks>
type FaultKind =
    /// The line could not be turned into a tree.
    | Syntax
    /// What was written does not fit what the command declared.
    | Binding
    | UnknownCommand
    /// A path, a variable or a record that is not there.
    | NotFound
    /// Something is already there, or the store would end up inconsistent.
    | Conflict
    /// The request was understood and is not allowed.
    | Invalid
    | Cancelled
    /// A defect. The message names the exception; it is never the user's mistake.
    | Internal

type Fault =
    { Kind: FaultKind
      Message: string
      /// Which stage of the pipeline failed, counted from one. Filled in by the
      /// evaluator, which is the only thing that knows.
      Stage: int option
      Path: string option
      Cause: Fault option }

/// The railway. `Ok` carries the value, `Error` carries the fault.
type Outcome<'T> = Result<'T, Fault>

[<RequireQualifiedAccess>]
module FaultKind =

    /// <summary>The kind as a word, the way `$problem.kind` reads it and a DTO carries it.</summary>
    /// <remarks>
    /// Written out rather than left to `ToString`, because this is what a script
    /// compares against — `where $problem.kind eq NotFound` — and a renamed case must
    /// not be able to change what a stored script means.
    /// </remarks>
    let name (kind: FaultKind) =
        match kind with
        | Syntax -> "Syntax"
        | Binding -> "Binding"
        | UnknownCommand -> "UnknownCommand"
        | NotFound -> "NotFound"
        | Conflict -> "Conflict"
        | Invalid -> "Invalid"
        | Cancelled -> "Cancelled"
        | Internal -> "Internal"

    /// The kind a word names, for reading one back out of a log.
    let tryParse (text: string) =
        match text with
        | "Syntax" -> Some Syntax
        | "Binding" -> Some Binding
        | "UnknownCommand" -> Some UnknownCommand
        | "NotFound" -> Some NotFound
        | "Conflict" -> Some Conflict
        | "Invalid" -> Some Invalid
        | "Cancelled" -> Some Cancelled
        | "Internal" -> Some Internal
        | _ -> None

[<RequireQualifiedAccess>]
module Fault =

    let create kind message =
        { Kind = kind; Message = message; Stage = None; Path = None; Cause = None }

    let withPath path fault = { fault with Path = Some path }

    /// Stamps the stage number on a fault that has not already been given one, so the
    /// innermost failure keeps its own position rather than the outermost overwriting it.
    let atStage stage fault =
        match fault.Stage with
        | Some _ -> fault
        | None -> { fault with Stage = Some stage }

    let causedBy cause fault = { fault with Cause = Some cause }

    // ---------------------------------------------------------------- Parse errors

    let syntax message = create Syntax message
    let couldNotParse () = create Syntax "Could not parse the command."

    /// <summary>The fault for a line that would not parse.</summary>
    /// <remarks>
    /// A grammar rule that knows why the input is wrong says so, and that sentence is
    /// what a person needs: `echo eq` is answered with how to write `eq` as text rather
    /// than with "could not parse". A rule that only knew what it was expecting falls
    /// back to the general message, which the browser replaces with the parser's own
    /// position and expected set.
    /// </remarks>
    let ofParseError (error: Commands.Parser.ParserError) =
        let explained (syntax: Commands.Parser.SyntaxError) =
            if System.String.IsNullOrEmpty syntax.Explanation then
                couldNotParse ()
            else
                create Syntax syntax.Explanation

        error.Match(
            (fun (messages: System.Collections.Generic.List<string>) ->
                if messages.Count = 0 then
                    couldNotParse ()
                else
                    create Syntax (System.String.Join(" ", messages))),
            explained,
            (fun (lexical: Commands.Parser.LexicalError) -> explained lexical.SyntaxError))

    // ------------------------------------------------------------- Argument errors

    let needsArgument command parameter =
        create Binding (sprintf "'%s' needs an argument for '%s'." command parameter)

    let tooManyArguments command declared given =
        create
            Binding
            (sprintf
                "'%s' takes %d argument%s, but %d were given."
                command
                declared
                (if declared = 1 then "" else "s")
                given)

    let noArgumentNamed command name =
        create Binding (sprintf "'%s' has no argument named '%s'." command name)

    /// Decision 0017: a command that declares no `Assignments` parameter says so,
    /// rather than ignoring what it was handed.
    let takesNoAssignments command name =
        create Binding (sprintf "'%s' does not take '%s=' assignments." command name)

    let unknownVariable name =
        create NotFound (sprintf "Unknown variable : $%s" name) |> withPath ("$" + name)

    let unsupportedArgument node =
        create Internal (sprintf "Unsupported argument value : %s" node)

    // ------------------------------------------------------------ Expression errors

    /// A node that cannot be one side of a comparison. A tag is the one that happens:
    /// `where $row.x eq <t/>` parses, and there is nothing sensible to compare against.
    let notAnOperand node =
        create Binding (sprintf "A %s cannot be part of an expression." node)

    let unknownOperator op =
        create Internal (sprintf "Unknown operator : %s" op)

    /// <summary>A pipeline in parentheses where no line is running to run it.</summary>
    /// <remarks>
    /// A nested pipeline runs once, when the line it is written in runs (decision 0023),
    /// and its value is what the command sees. A saved view is read back from a file
    /// with no line around it, so a pipeline inside one has nothing to run it and says
    /// so rather than being compared as its own text.
    /// </remarks>
    let nestedPipelineNotAValue text =
        create Invalid (sprintf "A pipeline in parentheses only runs as part of a line : %s" text)

    /// An expression only means something to a parameter that asked for one, so a
    /// command handed one it cannot use says so rather than comparing display strings.
    let takesNoExpression command parameter =
        create Binding (sprintf "'%s' takes a value for '%s', not an expression." command parameter)

    // ---------------------------------------------------------------- Table errors

    /// Decision 0009: a tag that is not table-shaped names the child that broke the
    /// shape, because "not a table" on its own leaves you counting children by hand.
    let notATable describe index reason =
        create Invalid (sprintf "%s is not a table: child %d %s." describe index reason)

    let needsATable command kind =
        create Binding (sprintf "'%s' needs a table, not %s." command kind)

    let noSuchColumn command name =
        create NotFound (sprintf "'%s' has no column named '%s'." command name)

    // ------------------------------------------------------------ Execution errors

    let unknownCommand name =
        create UnknownCommand (sprintf "Unknown command : %s" name)

    let directoryDoesNotExist path =
        create NotFound (sprintf "Directory does not exist : %s" path) |> withPath path

    let fileDoesNotExist path =
        create NotFound (sprintf "File does not exist : %s" path) |> withPath path

    let isADirectory path =
        create Invalid (sprintf "That is a directory, not a file : %s" path) |> withPath path

    let targetDirectoryExists path =
        create Conflict (sprintf "Target directory already exists : %s" path) |> withPath path

    let targetFileExists path =
        create Conflict (sprintf "Target file already exists : %s" path) |> withPath path

    let targetDirectoryDoesNotExist path =
        create NotFound (sprintf "Target directory does not exist : %s" path) |> withPath path

    let nothingExistsAt path =
        create NotFound (sprintf "Nothing exists at : %s" path) |> withPath path

    let directoryNotEmpty path =
        create Invalid (sprintf "Directory is not empty : %s" path) |> withPath path

    let cannotDeleteCurrentDirectory () =
        create Invalid "Cannot delete the current directory."

    let invalidVariableName name =
        create Invalid (sprintf "'%s' is not a valid variable name." name)

    let setNeedsValue name =
        create Binding (sprintf "'set' needs a value for '%s'." name)

    let stepsMustBePositive () = create Invalid "'steps' must be at least 1."

    /// Names the parameter it is about, because `progress` has two numbers and a
    /// message that always said `steps` sent you to the wrong one.
    let mustBeWhole parameter value =
        create Invalid (sprintf "'%s' must be a whole number, not '%s'." parameter value)

    /// A wait cannot be shorter than none, and `-1` to the runtime means "for ever",
    /// which is not what anybody typing it meant.
    let mustNotBeNegative parameter value =
        create Invalid (sprintf "'%s' must be zero or more, not '%s'." parameter value)

    /// <summary>A switch written as a word it does not take.</summary>
    /// <remarks>
    /// Any word used to count as on, so `sort name asc` sorted downwards. The message
    /// lists what the switch does take, which is the whole of what there is to know.
    /// </remarks>
    let notASwitchValue command parameter (allowed: string list) word =
        match allowed with
        | [ own ] -> create Binding (sprintf "'%s' takes '-%s' on its own, not '%s'." command own word)
        | _ ->
            let quoted = allowed |> List.map (sprintf "'%s'") |> String.concat " or "
            create Binding (sprintf "'%s' takes %s for '%s', not '%s'." command quoted parameter word)

    let notAValidUrl text = create Invalid (sprintf "Not a valid URL : %s" text)

    let noContentLength () = create Invalid "The server did not report a content length."

    // ------------------------------------------------------------- Record errors

    let fileNeedsAName () = create Invalid "A file must have a name."

    /// <summary>A name that could never be written as a path to the record it names.</summary>
    /// <remarks>
    /// A name is one segment of a path (decision 0016): `/` would make it two, and `.`
    /// and `..` already mean somewhere else, so a record called any of them could be
    /// listed and never reached.
    /// </remarks>
    let notAValidFileName name reason =
        create Invalid (sprintf "'%s' is not a valid file name: %s." name reason)

    /// <summary>`attr` asked to write an attribute the runtime owns (decision 0013).</summary>
    let setByTheTerminal name =
        create Invalid (sprintf "'%s' is set by the terminal and cannot be written." name)

    /// `size` is the content's length, worked out whenever it is asked for. A stored one
    /// would be a second `size` column that could disagree with the first.
    let computedFromContent name =
        create Invalid (sprintf "'%s' is worked out from the content and cannot be written." name)

    /// Decision 0016: a folder is a place other records name in their `folder`
    /// attribute, and a record that stopped being one would strand them.
    let directoryStaysADirectory path =
        create Invalid (sprintf "A directory cannot change its kind : %s" path) |> withPath path

    /// Decision 0016: a folder has no content, so a file with some cannot become one.
    let contentCannotBeADirectory path =
        create Invalid (sprintf "A file with content cannot become a directory : %s" path)
        |> withPath path

    /// `cp` copies one record. Copying a folder's record without what is in it made an
    /// empty folder that looked like a copy and was not one.
    let cannotCopyADirectory path =
        create Invalid (sprintf "'cp' copies files, and %s is a directory." path) |> withPath path

    // ----------------------------------------------------------- Evaluation errors

    let cannotEvaluate node = create Internal (sprintf "Cannot evaluate a %s." node)

    let cannotEvaluateChild node = create Internal (sprintf "Cannot evaluate a child %s." node)

    // -------------------------------------------------------------- Script errors

    /// A line in a script says which script and which line, counting every line in the
    /// file including the blank and commented ones, so the number matches an editor's.
    let inScript path line (fault: Fault) =
        { fault with Message = sprintf "%s line %d: %s" path line fault.Message }

    let scriptTooDeep depth =
        create Invalid (sprintf "Scripts are only allowed to run scripts %d deep." depth)

    // ----------------------------------------------------------------- View errors

    /// A command that only makes sense over a question says so rather than answering
    /// an empty listing, which is what a plain word would compare to `false` and give.
    let needsAPredicate command =
        create Binding (
            sprintf "'%s' needs a predicate, such as $row.kind eq note." command)

    /// A view file whose content is not a predicate any more. The text is quoted
    /// because the file is the only place it exists and the message is how you find it.
    let notAPredicate path text =
        create Invalid (sprintf "'%s' does not hold a predicate : %s" path text)
        |> withPath path

    /// <summary>A live refresh that would change something.</summary>
    /// <remarks>
    /// A refresh runs behind the user's back, outside a transaction and with nothing in
    /// the scrollback to show for it, so a line that writes must not be one. It is
    /// refused by what the line names rather than by what it turns out to do, because
    /// by the time it has done it the refusal is too late.
    /// </remarks>
    let refreshMustOnlyRead source =
        create Invalid (sprintf "A live refresh only re-reads : %s" source)

    // ------------------------------------------------------------ Document errors

    /// <summary>A file that `from-xml` could not read as XML.</summary>
    /// <remarks>
    /// The line and position are the parser's, which is where to look; the parser's own
    /// sentence is left out, because it is a resource the browser build does not carry
    /// and a message that reads differently on two hosts cannot be tested on either.
    /// </remarks>
    let notWellFormedXml path line position =
        create Invalid (sprintf "Not well-formed XML : %s line %d, position %d" path line position)
        |> withPath path

    /// XML is stricter about names than the notation is, and a table's columns can come
    /// from a CSV header that has spaces in it.
    let notAnXmlName name =
        create Invalid (sprintf "'%s' is not a name XML allows." name)

    let cannotWriteAsXml kind =
        create Binding (sprintf "'to-xml' needs a tag, a table or a list of tags, not %s." kind)

    /// A CSV record with more or fewer fields than the header: a table has one width.
    let csvFieldCount path line found expected =
        create Invalid (sprintf "%s line %d has %d fields where the header has %d." path line found expected)
        |> withPath path

    let csvUnclosedQuote path line =
        create Invalid (sprintf "%s line %d: a quoted field is never closed." path line)
        |> withPath path

    let csvTextAfterQuote path line =
        create Invalid (sprintf "%s line %d: a closing quote is followed by more text." path line)
        |> withPath path

    let csvDuplicateColumn path name =
        create Invalid (sprintf "%s has two columns named '%s'." path name) |> withPath path

    let csvUnnamedColumn path index =
        create Invalid (sprintf "%s column %d has no name." path index) |> withPath path

    let badDelimiter text =
        create Invalid (sprintf "'delimiter' must be one character, or 'tab', not '%s'." text)

    // ------------------------------------------------------------ Session messages

    let alreadyRunning () = create Invalid "A command is already running. Stop it first."

    let cancelled () = create Cancelled "Stopped."

    /// A defect that reached the session boundary. It is reported rather than swallowed
    /// so that a bug shows up as a message instead of ending the session.
    let internalError (exn: exn) =
        create Internal (sprintf "%s : %s" (exn.GetType().Name) exn.Message)

    // -------------------------------------------------------------- Store messages

    let notInitialised () =
        create Internal "The session has not been initialised. Call Initialize first."

    let nameAlreadyExists path =
        create Conflict (sprintf "Target file already exists : %s" path) |> withPath path

[<RequireQualifiedAccess>]
module Outcome =

    let ok value : Outcome<'T> = Ok value
    let fail fault : Outcome<'T> = Error fault

    let bind (f: 'a -> Outcome<'b>) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> f value
        | Error fault -> Error fault

    let map (f: 'a -> 'b) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok(f value)
        | Error fault -> Error fault

    let mapFault (f: Fault -> Fault) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok value
        | Error fault -> Error(f fault)

    /// The recovery combinator. `else` in the language (decision 0014) is this.
    let orElse (f: Fault -> Outcome<'a>) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> Ok value
        | Error fault -> f fault

    let ofOption (fault: Fault) (value: 'a option) =
        match value with
        | Some v -> Ok v
        | Option.None -> Error fault

    let defaultWith (fallback: 'a) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> value
        | Error _ -> fallback

    /// Threads a list, stopping at the first fault. Used wherever a command has a list
    /// of things each of which can fail, such as evaluating a tag's attributes.
    let traverse (f: 'a -> Outcome<'b>) (items: 'a list) : Outcome<'b list> =
        let rec loop acc remaining =
            match remaining with
            | [] -> Ok(List.rev acc)
            | head :: tail ->
                match f head with
                | Ok value -> loop (value :: acc) tail
                | Error fault -> Error fault

        loop [] items

    let sequence (items: Outcome<'a> list) : Outcome<'a list> = traverse id items

    /// Runs a side effect only on success, and keeps the outcome either way.
    let iter (f: 'a -> unit) (outcome: Outcome<'a>) =
        match outcome with
        | Ok value -> f value
        | Error _ -> ()

        outcome

type OutcomeBuilder() =
    member _.Bind(outcome, f) = Outcome.bind f outcome
    member _.Return value : Outcome<'T> = Ok value
    member _.ReturnFrom(outcome: Outcome<'T>) = outcome
    member _.Zero() : Outcome<unit> = Ok()
    member _.Delay(f: unit -> Outcome<'T>) = f
    member _.Run(f: unit -> Outcome<'T>) = f ()

    member _.Combine(first: Outcome<unit>, rest: unit -> Outcome<'T>) =
        match first with
        | Ok() -> rest ()
        | Error fault -> Error fault

    member _.TryWith(f: unit -> Outcome<'T>, handler) =
        try
            f ()
        with exn ->
            handler exn

    member _.For(items: seq<'a>, f: 'a -> Outcome<unit>) =
        let rec loop (e: System.Collections.Generic.IEnumerator<'a>) =
            if e.MoveNext() then
                match f e.Current with
                | Ok() -> loop e
                | Error fault -> Error fault
            else
                Ok()

        use enumerator = items.GetEnumerator()
        loop enumerator

/// <summary>
/// An async computation expression over `Outcome`, for commands.
/// </summary>
/// <remarks>
/// A command returns `Async&lt;Outcome&lt;_&gt;&gt;`, so binding a plain outcome inside
/// one otherwise means matching by hand at every step. Both bindings are offered: `let!`
/// on an `Outcome` and on an `Async&lt;Outcome&gt;`.
/// </remarks>
type AsyncOutcomeBuilder() =
    member _.Bind(outcome: Outcome<'a>, f: 'a -> Async<Outcome<'b>>) =
        match outcome with
        | Ok value -> f value
        | Error fault -> async.Return(Error fault)

    member _.Bind(computation: Async<Outcome<'a>>, f: 'a -> Async<Outcome<'b>>) =
        async {
            let! outcome = computation

            match outcome with
            | Ok value -> return! f value
            | Error fault -> return Error fault
        }

    member _.Bind(computation: Async<'a>, f: 'a -> Async<Outcome<'b>>) =
        async {
            let! value = computation
            return! f value
        }

    member _.Return value : Async<Outcome<'T>> = async.Return(Ok value)
    member _.ReturnFrom(computation: Async<Outcome<'T>>) = computation
    member _.ReturnFrom(outcome: Outcome<'T>) = async.Return outcome
    member _.Zero() : Async<Outcome<unit>> = async.Return(Ok())
    member _.Delay(f: unit -> Async<Outcome<'T>>) = async.Delay f

[<AutoOpen>]
module Builders =
    let outcome = OutcomeBuilder()
    let asyncOutcome = AsyncOutcomeBuilder()
