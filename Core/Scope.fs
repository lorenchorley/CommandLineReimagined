/// Variables.
///
/// A scope is a chain of maps. Today only the global scope exists and its contents are
/// the store's `Variables` projection, so `set` survives a reload for free once the log
/// persists. The chain is here because command bodies and table predicates will each
/// want a frame of their own.
namespace CommandLineReimagined.Core

open System

type Scope(variables: Map<string, Value>, parent: Scope option) =

    new(variables) = Scope(variables, Option.None)

    member _.Parent = parent

    /// This frame only.
    member _.Own = variables

    member this.TryFind(name: string) : Value option =
        match Map.tryFind name variables with
        | Some value -> Some value
        | Option.None ->
            match parent with
            | Some p -> p.TryFind name
            | Option.None -> Option.None

    /// A child frame. Binding in it leaves the parent alone.
    member this.Push() = Scope(Map.empty, Some this)

    member _.Bind (name: string) (value: Value) = Scope(Map.add name value variables, parent)

    member _.Unbind(name: string) = Scope(Map.remove name variables, parent)

    /// Everything visible, nearest frame winning, ordered by name so `vars` reads the
    /// same way twice.
    member this.All() : (string * Value) list =
        let rec collect (scope: Scope) acc =
            let acc =
                scope.Own
                |> Map.fold (fun (state: Map<string, Value>) name value ->
                    if Map.containsKey name state then state else Map.add name value state) acc

            match scope.Parent with
            | Some p -> collect p acc
            | Option.None -> acc

        collect this Map.empty |> Map.toList

    /// <summary>Whether a name may be bound.</summary>
    /// <remarks>
    /// The same rule the grammar uses for an identifier, checked here because `set`
    /// takes its name as a value and a value can have come from anywhere — a pipe, a
    /// variable, a quoted string.
    /// </remarks>
    static member IsValidName(name: string) =
        name.Length > 0
        && name |> Seq.forall (fun c -> Char.IsLetterOrDigit c || c = '_')

[<RequireQualifiedAccess>]
module Scope =

    /// The scope a line runs in: the store's variables, as a frame.
    let ofProjection (projection: Projection) = Scope projection.Variables
