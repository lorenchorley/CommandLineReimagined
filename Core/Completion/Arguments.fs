/// An argument, by what its parameter takes; and the signature hint (stream D).
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module ArgumentCompletion =

    /// <summary>What an argument could be.</summary>
    /// <remarks>
    /// Stream D dispatches on the slot and on `Parameter.Takes`: columns for a column,
    /// the switch's words for a switch, nothing for a count. Until then, the lexical
    /// rules, which offer files, and operators once there is a `$` in the stage.
    /// </remarks>
    let suggest (request: Request) (stage: Stage) (slot: Slot) : Async<Completion list> =
        ignore (stage, slot)
        async.Return(PathCompletion.suggest request false)

    /// <summary>The signature of the command the cursor is in, if it is in one.</summary>
    /// <remarks>
    /// Asked for every request, whatever the place: stream D builds it for `Argument`
    /// and `Predicate` places, with `Active` the parameter the word would bind to.
    /// </remarks>
    let signature (request: Request) : Signature option =
        ignore request
        None
