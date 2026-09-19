namespace CommandLineReimagined.Parsing

open System
open System.Collections.Generic
open FParsec
open Commands.Parser
open Commands.Parser.SemanticTree

/// <summary>
/// Parses a command line into the semantic tree, using FParsec.
/// </summary>
/// <remarks>
/// A drop-in replacement for CommandLineInterpreter: same input, same tree, same
/// ParserResult, so the shell and the web clients only change which parser they build.
///
/// There are no generated tables, so the grammar is the F# in Grammar.fs rather than a
/// .grm compiled to a .egt by an external tool and shipped as an embedded resource.
/// </remarks>
type CommandLineParser() =

    /// FParsec reports 1-based lines and columns. The error type, the prompt's
    /// highlighting and the web client all expect the 0-based offsets GOLD produced.
    static let positionOf (position: Position) = (int position.Line - 1, int position.Index)

    /// Pulls the "expected ..." labels out of FParsec's error tree, so callers get the
    /// same kind of information GOLD's ExpectedSymbols carried.
    static let expectedOf (messages: ErrorMessageList) : IReadOnlyList<string> =
        let found = HashSet<string>(StringComparer.Ordinal)
        let sorted: ErrorMessage[] = ErrorMessageList.ToSortedArray messages

        for message in sorted do
            match message with
            | :? ErrorMessage.Expected as expected -> found.Add expected.Label |> ignore
            | :? ErrorMessage.ExpectedString as expected -> found.Add expected.String |> ignore
            | _ -> ()

        ResizeArray(found) :> IReadOnlyList<string>

    static let toSyntaxError (error: FParsec.Error.ParserError) =
        let line, column = positionOf error.Position
        SyntaxError(Line = line, Column = column, ExpectedSymbols = expectedOf error.Messages)

    /// <summary>Parses a command line into a tree, or reports why it could not.</summary>
    member _.Parse<'TRoot>(source: string) : ParserResult<'TRoot> =
        let text = if isNull source then String.Empty else source

        // OneOf's implicit conversions are generated for C#; F# calls them by name.
        let ok (tree: 'TRoot) =
            ParserResult<'TRoot>(OneOf.OneOf<'TRoot, Commands.Parser.ParserError>.op_Implicit tree)

        let failed (error: Commands.Parser.ParserError) =
            ParserResult<'TRoot>(OneOf.OneOf<'TRoot, Commands.Parser.ParserError>.op_Implicit error)

        let syntaxFailure (error: FParsec.Error.ParserError) =
            let syntax = toSyntaxError error

            failed (
                Commands.Parser.ParserError(
                    OneOf.OneOf<List<string>, SyntaxError, LexicalError>.op_Implicit syntax))

        let messageFailure (message: string) =
            let messages = List<string>()
            messages.Add message

            failed (
                Commands.Parser.ParserError(
                    OneOf.OneOf<List<string>, SyntaxError, LexicalError>.op_Implicit messages))

        try
            // The requested root type selects the grammar, the way the GOLD version
            // chose between its two parsers.
            if typeof<'TRoot> = typeof<Identifier> then
                match runParserOnString Grammar.identifierOnly () "command" text with
                | Success(result, _, _) -> ok (box result :?> 'TRoot)
                | Failure(_, error, _) -> syntaxFailure error
            else
                match runParserOnString Grammar.program () "command" text with
                | Success(result, _, _) -> ok (box result :?> 'TRoot)
                | Failure(_, error, _) -> syntaxFailure error
        with exn ->
            // A mismatched closing tag is raised from inside the grammar: the shape is
            // valid and only the names disagree, so it is a message rather than a
            // position. The GOLD interpreter reported it the same way.
            messageFailure exn.Message

    /// <summary>Whether the text is a single valid identifier.</summary>
    member this.IsValidIdentifier(text: string) : bool = this.Parse<Identifier>(text).IsT0

    /// <summary>The syntax error for a command line, or null when it parses.</summary>
    member this.HasSyntaxError(text: string) : SyntaxError =
        let result = this.Parse<RootNode>(text)

        if result.IsT0 then
            null
        else
            result.AsT1.Match(
                (fun (_: List<string>) -> null),
                (fun (syntax: SyntaxError) -> syntax),
                (fun (lexical: LexicalError) -> lexical.SyntaxError))
