/// What a value is, in one line (Phase 8).
///
/// For a completion chip's detail, for hover and for `vars`, so that all three describe
/// a value in the same words.
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module Summary =

    let private limit = 40

    let private cut (text: string) =
        if text.Length <= limit then text else text.Substring(0, limit - 1) + "…"

    /// <summary>The kind of a value, and its display text cut short.</summary>
    /// <remarks>
    /// Stream C makes this right for each kind: rows and columns for a table, the kind
    /// and message for a fault. Until then it is `number · 5`, `table · …`.
    /// </remarks>
    let ofValue (value: Value) : string =
        let kind = Value.kind value
        let text = Value.display value

        if text.Length = 0 then kind else kind + " · " + cut text
