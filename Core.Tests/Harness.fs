/// Shared helpers. Nothing here asserts; it builds the things the tests assert about.
module CommandLineReimagined.Core.Tests.Harness

open System
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core

/// A clock that does not move, so a test can compare two transactions without a
/// timestamp making them differ.
let fixedClock () =
    let at = DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero)
    fun () -> at

/// Runs an async computation. WebAssembly is single threaded and the core never
/// blocks; the tests are not WebAssembly, so this is the one place that does.
let run (computation: Async<'T>) = Async.RunSynchronously computation

let newStore () = Store(InMemoryLog(), fixedClock ())

let initialised () =
    let store = newStore ()
    run (store.Initialize())
    store

let text value = Value.Text value

let record id name kind folder =
    { Id = id
      Attributes =
        Map.ofList
            [ Attributes.name, Value.Text name
              Attributes.kind, Value.Text kind
              Attributes.folder, Value.Text folder ]
      Content = None }

let file id name folder = record id name "text" folder
let folder id name parent = record id name Value.folderKind parent

/// Asserts that an outcome succeeded, and hands back what it carried. The message
/// names the fault, so a failing test says what went wrong rather than "expected Ok".
let expectOk (outcome: Outcome<'T>) : 'T =
    match outcome with
    | Ok value -> value
    | Error fault -> raise (AssertFailedException(sprintf "Expected success, got: %s" fault.Message))

/// Asserts that an outcome failed with a given kind, and hands back the fault.
let expectFault (kind: FaultKind) (outcome: Outcome<'T>) : Fault =
    match outcome with
    | Ok _ -> raise (AssertFailedException(sprintf "Expected a %A fault, but it succeeded." kind))
    | Error fault ->
        Assert.AreEqual<FaultKind>(kind, fault.Kind, sprintf "Wrong fault kind for '%s'." fault.Message)
        fault

/// <summary>
/// A projection with something in it: `/notes.txt` holding content, `/documents`, and
/// a variable `$v`.
/// </summary>
/// <remarks>
/// Shared by the tests that need an inverse to have something to restore, rather than
/// trivially landing back on empty.
/// </remarks>
let populated =
    Projection.applyAll
        Projection.empty
        [ FileCreated(file "a" "notes.txt" "/")
          FileCreated(folder "b" "documents" "/")
          ContentChanged("a", None, Some "old")
          VariableChanged("v", None, Some(Value.Number 1.0)) ]

/// <summary>Every event shape, each one applicable to <c>populated</c>.</summary>
/// <remarks>
/// "Applicable" is the whole of the contract an inverse depends on: an event's
/// `before` side must be what the projection actually holds. A `FileCreated` for a
/// record that is already there, or a `ContentChanged` whose `before` is not the
/// current hash, does not describe a change that happened, and inverting it would put
/// back a past that never was. Commands build their events from the current
/// projection, so they never produce one, and the store validates the one case it can
/// check cheaply. Listing the shapes here rather than writing a test per case is what
/// makes a new event case fail loudly instead of quietly going unchecked.
/// </remarks>
let everyEventShape () =
    [ FileCreated(file "new" "scratch.txt" "/")
      FileDeleted(folder "b" "documents" "/")
      AttributesChanged(
          "a",
          populated.Files["a"].Attributes,
          Map.add "tag" (Value.Text "work") populated.Files["a"].Attributes
      )
      ContentChanged("a", Some "old", Some "new")
      ContentChanged("a", Some "old", None)
      VariableChanged("w", None, Some(Value.Number 2.0))
      VariableChanged("v", Some(Value.Number 1.0), None)
      VariableChanged("v", Some(Value.Number 1.0), Some(Value.Number 9.0))
      LocationChanged({ Folder = "/"; View = None }, { Folder = "/documents"; View = None }) ]
