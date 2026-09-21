/// Running whole command lines against a session, the way a user would.
module CommandLineReimagined.Core.Tests.SessionHarness

open System
open System.Net.Http
open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core

/// <summary>A session over an in-memory log, with everything pinned.</summary>
/// <remarks>
/// Ids count up and the clock does not move, so a test can assert on a projection and
/// on `history` without a GUID or a timestamp making two equal runs look different.
/// </remarks>
type Harness(?seedFiles: SeedFile list, ?httpClient: unit -> HttpClient) =
    let mutable counter = 0

    let newId () =
        counter <- counter + 1
        sprintf "id-%d" counter

    let clock () = DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero)

    let options =
        { SessionOptions.defaults with
            Clock = clock
            NewId = newId
            HttpClient = defaultArg httpClient (fun () -> new HttpClient()) }

    let seed =
        match seedFiles with
        | Some files -> Seed.ofFiles newId clock files
        | None -> Seed.none

    let session = Session(InMemoryLog(), options, seed)

    do Async.RunSynchronously(session.Initialize())

    member _.Session = session

    member _.Projection = session.Projection

    member _.Location = session.Location.Folder

    /// Runs a line and returns the whole response.
    member _.Respond(line: string) = Async.RunSynchronously(session.Execute line)

    /// Runs a line expected to succeed, and returns its value.
    member this.Run(line: string) : Value =
        let response = this.Respond line

        match response.Fault with
        | Some fault ->
            raise (AssertFailedException(sprintf "'%s' failed: %s" line fault.Message))
        | None -> defaultArg response.Result Value.Empty

    /// Runs a line expected to fail, and returns the fault.
    member this.Fail(line: string) : Fault =
        let response = this.Respond line

        match response.Fault with
        | Some fault -> fault
        | None -> raise (AssertFailedException(sprintf "'%s' was expected to fail, and did not." line))

    /// Runs a line expected to fail, and returns the message.
    member this.Error(line: string) = (this.Fail line).Message

    /// What the line displayed as its result.
    member this.Text(line: string) = Value.display (this.Run line)

    /// What commands wrote while the last line ran.
    member this.Written(line: string) = (this.Respond line).Output

    member _.Variable(name: string) = Map.tryFind name session.Projection.Variables

    member _.Exists(path: string) =
        Files.resolve session.Projection session.Location path |> Result.isOk

    /// The content of a file, read back through the blob store.
    member _.Content(path: string) =
        match Files.resolve session.Projection session.Location path with
        | Error fault -> failwith fault.Message
        | Ok record ->
            match record.Content with
            | None -> ""
            | Some hash -> defaultArg (Async.RunSynchronously(session.Store.GetBlob hash)) ""

    member _.Attribute (path: string) (name: string) =
        match Files.resolve session.Projection session.Location path with
        | Error fault -> failwith fault.Message
        | Ok record -> Attributes.text record.Attributes name

    /// The sources of every committed line, oldest first.
    member _.History() =
        session.History() |> List.map (fun entry -> entry.Transaction.Source)

    member _.Undone() =
        session.History()
        |> List.filter (fun entry -> entry.Undone)
        |> List.map (fun entry -> entry.Transaction.Source)

/// The filesystem the acceptance list starts from: the standard seed.
let seeded () = Harness(Seed.standardFiles)

/// A session with only what a test puts in it.
let bare () = Harness()
