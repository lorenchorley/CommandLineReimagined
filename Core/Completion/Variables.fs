/// Variables, their members, and tags (stream C).
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module VariableCompletion =

    /// <summary>After `$`: the variables in scope.</summary>
    /// <remarks>
    /// Stream C gives each its summary as the detail, and offers `$row` only where
    /// `inPredicate`. Until then, the lexical rules.
    /// </remarks>
    let variables (request: Request) (inPredicate: bool) : Async<Completion list> =
        ignore inPredicate
        async.Return(Lexical.answer request)

    /// <summary>After `$name.`: the members of what the variable holds.</summary>
    /// <remarks>
    /// `$row.` asks `request.Shapes stage`. Until stream C, every variable answers the
    /// columns of a listing of the current folder, as the lexical rules did.
    /// </remarks>
    let members (request: Request) (variable: string) (path: string list) (stage: Stage option) : Async<Completion list> =
        ignore (variable, path, stage)
        async.Return(Lexical.answer request)

    /// After `<`: the tag types in use. The lexical rules until stream C.
    let tagTypes (request: Request) : Async<Completion list> = async.Return(Lexical.answer request)

    /// Inside `<type `: the attribute names records of that type carry, as `name=`.
    /// The lexical rules until stream C.
    let tagAttributes (request: Request) (typeName: string) : Async<Completion list> =
        ignore typeName
        async.Return(Lexical.answer request)
