/// What the text could continue as.
///
/// Phone keyboards have no Tab key, so this is the only completion most users will
/// have: the page shows the answers as chips and substitutes one on a tap. From
/// Phase 8 it reads the line (decision 0031): `Context` says what kind of place the
/// cursor is in, and each kind has its own provider. This file only dispatches.
namespace CommandLineReimagined.Core

open System.Threading

[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Completion =

    /// Everything a provider is given, for a line and a cursor.
    let request (specs: CommandSpec list) (source: ShapeSource) (text: string) (cursor: int) (cancel: CancellationToken) =
        { Specs = specs
          Projection = source.Projection
          Context = Context.analyse specs text cursor
          Shapes = Shape.ofUpstream source
          Cancel = cancel }

    /// Whether the stage before the cursor is a variable standing alone, such as `$files `.
    let private valueStage =
        System.Text.RegularExpressions.Regex(@"(?:^|\|)\s*\$[A-Za-z_][A-Za-z0-9_-]*(?:\.[A-Za-z_][A-Za-z0-9_-]*)*\s+$")

    let private valueStageBefore (context: Context) =
        valueStage.IsMatch(context.Text.Substring(0, min context.Cursor context.Text.Length))

    /// Asks the provider for the place the cursor is in.
    let complete (request: Request) : Async<CompletionResult> =
        async {
            let! items =
                match request.Context.Place with
                // Nothing typed yet: offering every command is noise, and the page
                // shows its suggestion chips in that state instead.
                | Place.Blank -> async.Return []
                | Place.CommandName(afterPipe, upstream) -> CommandCompletion.suggest request afterPipe upstream
                | Place.Variable inPredicate -> VariableCompletion.variables request inPredicate
                | Place.Member(variable, path, stage) -> VariableCompletion.members request variable path stage
                | Place.TagType -> VariableCompletion.tagTypes request
                | Place.TagAttribute typeName -> VariableCompletion.tagAttributes request typeName
                | Place.Argument(stage, slot) -> ArgumentCompletion.suggest request stage slot
                // A whole question is a complete argument, so the pipe comes first
                // there as it does after any complete stage (decision 0039).
                | Place.Predicate(stage, (Expression.AfterComparison as expression)) when request.Context.Word.Prefix = "" ->
                    async {
                        let! rest = PredicateCompletion.suggest request stage expression
                        return ArgumentCompletion.pipe request :: rest
                    }
                // A plain name after `in` is a place, and already a whole argument, so
                // the pipe comes before the operators that would make it a question.
                | Place.Predicate(stage, (Expression.AfterOperand(Expr.Const _) as expression)) when
                    request.Context.Word.Prefix = ""
                    && stage.Spec
                       |> Option.exists (fun spec -> spec.Parameters |> List.exists (fun p -> p.Takes = Takes.Place))
                    ->
                    async {
                        let! rest = PredicateCompletion.suggest request stage expression
                        return ArgumentCompletion.pipe request :: rest
                    }
                | Place.Predicate(stage, expression) -> PredicateCompletion.suggest request stage expression
                // A variable standing as a stage (decision 0032) is a whole stage, so the
                // pipe comes first after it (0039), before what the lexical rules offer.
                | Place.Unknown when request.Context.Word.Prefix = "" && valueStageBefore request.Context ->
                    async.Return(ArgumentCompletion.pipe request :: Lexical.answer request)
                | Place.Unknown -> async.Return(Lexical.answer request)

            return
                { Items = items
                  Signature = ArgumentCompletion.signature request }
        }
