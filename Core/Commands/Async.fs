/// The long-running commands: progress and download.
///
/// They are the reason `IOutput` exists. A command's result is returned at the end; a
/// progress bar has to appear while it is still going, so it writes to its output and
/// the host decides what showing that means.
module CommandLineReimagined.Core.Commands.Async

open System
open System.Diagnostics
open System.Net.Http
open CommandLineReimagined.Core

/// A number written for a parameter that has to be whole. A bare `-steps` flag binds
/// `true`, which is the case the message names, and the message names the parameter
/// it is about, since there are two.
let private whole (name: string) (fallback: int) (invocation: Invocation) : Outcome<int> =
    if not (Invocation.given name invocation) then
        Ok fallback
    else
        match Invocation.value name invocation with
        | Value.Number n when Double.IsInteger n -> Ok(int n)
        | other -> Error(Fault.mustBeWhole name (Value.display other))

let progress =
    { Spec =
        CommandSpec.create
            "progress"
            "Runs a progress bar, to exercise async commands, cancellation and undo"
            [ "progress"; "test"; "bar" ]
            [ Parameter.optional "steps" "How many steps to take; 100 by default" |> Parameter.takes Takes.Count
              Parameter.optional "delay" "Milliseconds between steps; 100 by default" |> Parameter.takes Takes.Number ]
      Run =
        fun invocation ->
            async {
                match whole "steps" 100 invocation, whole "delay" 100 invocation with
                | Error fault, _
                | _, Error fault -> return Error fault
                | Ok steps, Ok delay ->
                    if steps <= 0 then
                        return Error(Fault.stepsMustBePositive ())
                    // Refused before it reaches the runtime, which throws on most
                    // negative waits and takes -1 to mean "for ever".
                    elif delay < 0 then
                        return Error(Fault.mustNotBeNegative "delay" (string delay))
                    else
                        let counter = invocation.Output.NewLine().Write "0%"
                        let bar = invocation.Output.NewLine().Write ""
                        let mutable percentage = 0
                        let mutable cancelled = false
                        let mutable step = 1

                        while step <= steps && not cancelled do
                            if invocation.Cancel.IsCancellationRequested then
                                cancelled <- true
                            else
                                do! Async.Sleep delay
                                percentage <- int (Math.Round(100.0 * float step / float steps))
                                counter.Text <- $"{percentage}%%"
                                bar.Text <- String('=', percentage / 4) + ">"
                                step <- step + 1

                        if cancelled then
                            invocation.Output.NewLine().Write($"Cancelled at {percentage}%%") |> ignore
                            return Error(Fault.cancelled ())
                        else
                            invocation.Output.NewLine().Write "Progress test finished" |> ignore
                            return Invocation.pure' (Value.Number(float percentage))
            } }

/// Small and served with permissive CORS headers, so the browser build can fetch it
/// too. The original default was a four-gigabyte Ubuntu image.
let defaultUrl =
    "https://raw.githubusercontent.com/lorenchorley/CommandLineReimagined/main/README.md"

/// <summary>
/// Downloads a URL into a file, reporting progress as it goes.
/// </summary>
/// <remarks>
/// The client is passed in rather than created, so a test can answer without a network
/// and the browser can use the one Blazor configured. The file it writes is a record
/// like any other, so the download is undone by the same mechanism as everything else.
/// </remarks>
let download (client: unit -> HttpClient) (newId: Files.IdSource) (now: unit -> DateTimeOffset) =
    { Spec =
        CommandSpec.create
            "download"
            "Download a file, with progress"
            [ "download"; "file"; "transfer"; "api"; "stream"; "http"; "ftp" ]
            [ Parameter.optional "url" "What to download" |> Parameter.takes Takes.Url
              Parameter.optional "into" "Directory to download into" |> Parameter.takes Takes.Place ]
      Run =
        fun invocation ->
            async {
                let url = Invocation.textOr defaultUrl "url" invocation

                let into =
                    Invocation.textOr invocation.Location.Folder "into" invocation

                match Uri.TryCreate(url, UriKind.Absolute) with
                | false, _ -> return Error(Fault.notAValidUrl url)
                | true, uri ->
                    match Files.resolveFolder invocation.Projection invocation.Location into with
                    | Error _ ->
                        return
                            Error(
                                Fault.targetDirectoryDoesNotExist (Files.normalise invocation.Location.Folder into)
                            )
                    | Ok folder ->
                        let name =
                            let candidate = uri.LocalPath.TrimEnd '/'

                            match candidate.LastIndexOf '/' with
                            | -1 -> candidate
                            | index -> candidate.Substring(index + 1)

                        if name = "" then
                            return Error(Fault.notAValidUrl url)
                        else
                            let counter = invocation.Output.NewLine().Write "0%"
                            let bar = invocation.Output.NewLine().Write ""
                            let speed = invocation.Output.NewLine().Write ""

                            try
                                use http = client ()

                                let! response =
                                    http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, invocation.Cancel)
                                    |> Async.AwaitTask

                                match response.Content.Headers.ContentLength |> Option.ofNullable with
                                | None -> return Error(Fault.noContentLength ())
                                | Some total ->
                                    let! text = response.Content.ReadAsStringAsync invocation.Cancel |> Async.AwaitTask

                                    // The whole body, then progress reported once. A
                                    // chunked read that wrote to a growing blob would
                                    // report more finely and would also mean a partial
                                    // file surviving a cancelled line, which decision
                                    // 0015 says must not happen.
                                    counter.Text <- "100%"
                                    bar.Text <- String('=', 100) + ">"

                                    let stopwatch = Stopwatch.GetTimestamp()
                                    ignore stopwatch
                                    speed.Text <- $"{float total / 1024.0 / 1024.0:F2} MB"

                                    // Written the way `write` writes, so a download over
                                    // an existing file touches `modified` as a write does
                                    // and a new one gets the same attributes.
                                    let! written =
                                        Files.writeContent newId now invocation (Value.joinPath folder name) None text

                                    if Result.isOk written then
                                        invocation.Output.NewLine().Write($"Downloaded to {Value.joinPath folder name}")
                                        |> ignore

                                    return written
                            with
                            | :? OperationCanceledException -> return Error(Fault.cancelled ())
                            | exn -> return Error(Fault.create Invalid $"Download failed : {exn.Message}")
            } }
