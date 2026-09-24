namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Queries as places: `in` on a predicate, `find`, `save-view`, live refresh.</summary>
/// <remarks>
/// Decision 0013's last step. A folder is one question among many — "which records say
/// they are in /journal" — so the thing `in` takes is a question rather than a path,
/// and a folder path is the answer you get when the question is a plain name. These
/// tests are about the difference between the two and about what stays true across it:
/// new files still land in a folder, undo still restores where you were, and asking a
/// question still leaves no trace.
/// </remarks>
[<TestClass>]
type ViewTests() =

    /// A journal in one folder and a note in another, so a view that lists across
    /// folders is distinguishable from one that does not.
    let journal () =
        let harness = seeded ()
        harness.Run "mkdir journal" |> ignore
        harness.Run "in journal" |> ignore
        harness.Run "save <note name=monday mood=good tag=work/>" |> ignore
        harness.Run "save <note name=saturday mood=great tag=home/>" |> ignore
        harness.Run "out" |> ignore
        harness.Run "save <note name=stray mood=great tag=home/>" |> ignore
        harness

    // ------------------------------------------------------------------- in

    [<TestMethod>]
    member _.InOnAPlainNameIsStillAFolder() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)
        Assert.AreEqual<Expr option>(None, harness.View)

    /// A folder is answered as the record, so the result is a chip you can tap and a
    /// value you can pipe, rather than a string that happens to look like a path.
    [<TestMethod>]
    member _.InAnswersTheFolderItEntered() =
        let harness = seeded ()

        match harness.Run "in documents" with
        | Value.File file ->
            Assert.AreEqual<string>("documents", file.Name)
            Assert.AreEqual<string>(Value.folderKind, file.Kind)
        | other -> Assert.Fail(sprintf "in answered %s, not a folder." (Value.kind other))

    /// The root has no record (decision 0016), so it is the one folder that answers
    /// with its path.
    [<TestMethod>]
    member _.InToTheRootAnswersItsPath() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore

        Assert.AreEqual<Value>(Value.Text "/", harness.Run "in /")

    [<TestMethod>]
    member _.InOnAPredicateSetsTheView() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore

        Assert.AreEqual<string>("$row.mood eq great", harness.ViewText)

    /// The folder is left alone: a view is a way of looking, not a place files go.
    [<TestMethod>]
    member _.AViewLeavesTheFolderWhereItWas() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore

        Assert.AreEqual<string>("/journal", harness.Location)

    [<TestMethod>]
    member _.InOnAPredicateAnswersThePredicate() =
        let harness = journal ()

        Assert.AreEqual<string>("$row.mood eq great", harness.Text "in $row.mood eq great")

    /// Entering a folder puts down whatever view was held: `ls` cannot list two things.
    [<TestMethod>]
    member _.EnteringAFolderClearsTheView() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "in journal" |> ignore

        Assert.AreEqual<Expr option>(None, harness.View)

    // ------------------------------------------------------------------- ls

    /// The point of the whole phase: a view is not scoped to a folder, so a listing of
    /// one crosses them and the `folder` column says where each row came from.
    [<TestMethod>]
    member _.AViewListsAcrossFolders() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore

        Assert.AreEqual<string>("saturday stray", harness.Names "ls")

        Assert.AreEqual<string list>(
            [ "/journal"; "/" ],
            harness.Column "ls" "folder")

    /// Only the matching records' attributes become columns, so a view over three
    /// notes does not carry a column for every attribute in the store.
    [<TestMethod>]
    member _.AViewsColumnsAreTheMatchingRecordsAttributes() =
        let harness = journal ()
        harness.Run "in $row.kind eq note and $row.tag eq work" |> ignore

        Assert.AreEqual<string list>(
            [ "name"; "kind"; "folder"; "size"; "modified"; "mood"; "tag" ],
            Table.names (harness.Table "ls"))

    /// Writing a path is how you look somewhere else without leaving the view.
    [<TestMethod>]
    member _.APathOverridesTheViewWithoutLeavingIt() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore

        Assert.AreEqual<string>("monday saturday", harness.Names "ls /journal")
        Assert.AreEqual<string>("$row.mood eq great", harness.ViewText)

    /// New files are created in the folder, never "in a view".
    [<TestMethod>]
    member _.NewFilesStillLandInTheFolder() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "mkdir inbox" |> ignore

        Assert.AreEqual<string>("/journal", harness.Attribute "/journal/inbox" "folder")

    // ------------------------------------------------------------------ out

    [<TestMethod>]
    member _.OutClearsTheView() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "out" |> ignore

        Assert.AreEqual<Expr option>(None, harness.View)
        Assert.AreEqual<string>("/journal", harness.Location)

    /// Putting the view down answers where you turn out to be, which is a path rather
    /// than a record: it is the folder you were already in, not one you just entered.
    [<TestMethod>]
    member _.OutFromAViewAnswersTheFolderPath() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore

        Assert.AreEqual<Value>(Value.Text "/journal", harness.Run "out")

    /// Two `out`s from a view over a subfolder come out in the order they went in.
    [<TestMethod>]
    member _.OutTwiceLeavesTheViewThenTheFolder() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "out" |> ignore
        harness.Run "out" |> ignore

        Assert.AreEqual<string>("/", harness.Location)

    [<TestMethod>]
    member _.OutWithoutAViewStillMovesUp() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "out" |> ignore

        Assert.AreEqual<string>("/", harness.Location)

    // ------------------------------------------------------------------ pwd

    [<TestMethod>]
    member _.PwdAnswersTheViewWhenThereIsOne() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore

        match harness.Run "pwd" with
        | Value.Query expr -> Assert.AreEqual<string>("$row.mood eq great", Expr.display expr)
        | other -> Assert.Fail(sprintf "pwd answered %s, not a query." (Value.kind other))

    [<TestMethod>]
    member _.PwdAnswersTheFolderWhenThereIsNoView() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore

        Assert.AreEqual<Value>(Value.Text "/documents", harness.Run "pwd")

    // ----------------------------------------------------------------- undo

    /// A view is entered by an event like any other move, so taking it back is the
    /// store's ordinary undo rather than anything views know about.
    [<TestMethod>]
    member _.UndoRestoresWhereYouWere() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<Expr option>(None, harness.View)
        Assert.AreEqual<string>("/journal", harness.Location)

    [<TestMethod>]
    member _.UndoPutsAClearedViewBack() =
        let harness = journal ()
        harness.Run "in $row.mood eq great" |> ignore
        harness.Run "out" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("$row.mood eq great", harness.ViewText)

    // ----------------------------------------------------------------- find

    [<TestMethod>]
    member _.FindListsAcrossTheWholeStore() =
        let harness = journal ()

        Assert.AreEqual<string>("saturday stray", harness.Names "find $row.mood eq great")

    /// The whole difference between `find` and `in`: asking a question is not going
    /// anywhere, so nothing is committed and `undo` reaches past it.
    [<TestMethod>]
    member _.FindDoesNotChangeTheLocation() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "find $row.mood eq great" |> ignore

        Assert.AreEqual<string>("/journal", harness.Location)
        Assert.AreEqual<Expr option>(None, harness.View)

    [<TestMethod>]
    member _.FindCommitsNothing() =
        let harness = journal ()
        let before = harness.History()
        harness.Run "find $row.mood eq great" |> ignore

        Assert.AreEqual<string list>(before, harness.History())

    [<TestMethod>]
    member _.FindIsATableLikeAnyOther() =
        let harness = journal ()

        Assert.AreEqual<string>("2", harness.Text "find $row.mood eq great | count")

    /// A plain word is not a question, and an empty listing is a worse answer than a
    /// message saying so.
    [<TestMethod>]
    member _.FindNeedsAPredicate() =
        let harness = journal ()

        StringAssert.Contains(harness.Error "find monday", "needs a predicate")

    // ----------------------------------------------------------- save-view

    [<TestMethod>]
    member _.ASavedViewIsAFile() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore

        Assert.AreEqual<string>(Value.viewKind, harness.Attribute "/weekend" "kind")
        Assert.AreEqual<string>("$row.mood eq great", harness.Content "/weekend")

    [<TestMethod>]
    member _.ASavedViewAppearsInTheListing() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore

        StringAssert.Contains(harness.Names "ls", "weekend")

    [<TestMethod>]
    member _.InOnASavedViewEntersIt() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore
        harness.Run "in weekend" |> ignore

        Assert.AreEqual<string>("$row.mood eq great", harness.ViewText)
        Assert.AreEqual<string>("saturday stray", harness.Names "ls")

    /// A view is a record, so undo takes it away like any other file.
    [<TestMethod>]
    member _.UndoRemovesASavedView() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore
        harness.Run "undo" |> ignore

        Assert.IsFalse(harness.Exists "/weekend")

    [<TestMethod>]
    member _.SaveViewRefusesANameThatIsTaken() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore

        StringAssert.Contains(harness.Error "save-view weekend $row.mood eq good", "already exists")

    [<TestMethod>]
    member _.SaveViewNeedsAPredicate() =
        let harness = journal ()

        StringAssert.Contains(harness.Error "save-view weekend monday", "needs a predicate")

    /// A view file someone has written nonsense into says which file and what it holds,
    /// because the file is the only place that text exists.
    [<TestMethod>]
    member _.AViewFileThatIsNotAPredicateSaysSo() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore
        harness.Run "echo \"not a predicate at all\" | write weekend" |> ignore

        let fault = harness.Fail "in weekend"

        Assert.AreEqual<FaultKind>(Invalid, fault.Kind)
        StringAssert.Contains(fault.Message, "/weekend")

    /// Text that parses but asks nothing — one word — is not a view either. `in` used to
    /// enter it as a question every record answered `false` to, where `save-view` and
    /// `find` refuse the same text.
    [<TestMethod>]
    member _.AViewFileHoldingAPlainWordIsNotEntered() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore
        harness.Run "echo monday | write weekend" |> ignore

        Assert.AreEqual<string>("'/weekend' does not hold a predicate : monday", harness.Error "in weekend")
        Assert.AreEqual<Expr option>(None, harness.View)

    /// A view file whose question never reads `$row` gets the check `in` makes on a
    /// predicate written out (decision 0033), rather than listing nothing in silence.
    [<TestMethod>]
    member _.AViewFileThatNeverReadsTheRowIsRefused() =
        let harness = journal ()
        harness.Run "save-view weekend $row.mood eq great" |> ignore
        harness.Run "echo \"mood eq great\" | write weekend" |> ignore

        let fault = harness.Fail "in weekend"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)

        Assert.AreEqual<string>("mood eq great never reads $row, so it is the same for every row.", fault.Message)

        Assert.AreEqual<string list>(
            [ "Did you mean $row.mood eq great?" ],
            fault.Notes |> List.map (fun note -> note.Text))

        Assert.AreEqual<Expr option>(None, harness.View)

    // -------------------------------------------------------------- refresh

    /// What a live listing is made of: the same answer, computed again, with nothing
    /// left behind.
    [<TestMethod>]
    member _.RefreshReReadsAListing() =
        let harness = journal ()
        harness.Run "in journal" |> ignore

        let response = harness.Refresh "ls"

        Assert.AreEqual<Fault option>(None, response.Fault)

        match response.Result with
        | Some(Value.Table table) -> Assert.AreEqual<int>(2, List.length table.Rows)
        | other -> Assert.Fail(sprintf "A refreshed listing answered %A." other)

    [<TestMethod>]
    member _.RefreshSeesWhatHasChangedSince() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        harness.Run "mkdir inbox" |> ignore

        match (harness.Refresh "ls").Result with
        | Some(Value.Table table) -> Assert.AreEqual<int>(3, List.length table.Rows)
        | other -> Assert.Fail(sprintf "A refreshed listing answered %A." other)

    [<TestMethod>]
    member _.RefreshCommitsNothing() =
        let harness = journal ()
        let before = harness.History()
        harness.Refresh "ls" |> ignore

        Assert.AreEqual<string list>(before, harness.History())

    /// Refused by what the line names, before it runs: a refresh happens behind the
    /// user's back and there is nothing in the scrollback to undo it from.
    [<TestMethod>]
    member _.RefreshRefusesAMutatingLine() =
        let harness = journal ()
        let response = harness.Refresh "mkdir sneaky"

        match response.Fault with
        | Some fault ->
            Assert.AreEqual<FaultKind>(Invalid, fault.Kind)
            StringAssert.Contains(fault.Message, "only re-reads")
        | None -> Assert.Fail "A refresh of 'mkdir' should have been refused."

        Assert.IsFalse(harness.Exists "/sneaky", "The refused refresh must not have created anything.")

    /// `out` moves you, which is a change: it lands in `history` and `undo` takes it back.
    /// It used to be marked read-only, which let a live refresh name it.
    [<TestMethod>]
    member _.RefreshRefusesOut() =
        let harness = journal ()
        harness.Run "in journal" |> ignore

        match (harness.Refresh "out").Fault with
        | Some fault -> Assert.AreEqual<string>("A live refresh only re-reads : out", fault.Message)
        | None -> Assert.Fail "A refresh of 'out' should have been refused."

    /// Refused for the whole line, not stage by stage: a pipeline that reads and then
    /// writes is a writing line.
    [<TestMethod>]
    member _.RefreshRefusesAPipelineThatEndsInAWrite() =
        let harness = journal ()

        match (harness.Refresh "ls | set listing").Fault with
        | Some fault -> StringAssert.Contains(fault.Message, "only re-reads")
        | None -> Assert.Fail "A refresh ending in 'set' should have been refused."

    [<TestMethod>]
    member _.RefreshAllowsAWholeReadingPipeline() =
        let harness = journal ()
        harness.Run "in journal" |> ignore

        let response = harness.Refresh "ls | where $row.mood eq great | count"

        Assert.AreEqual<Fault option>(None, response.Fault)
        Assert.AreEqual<string>("1", Value.display (defaultArg response.Result Value.Empty))

    /// An unknown name is refused rather than reported: a refresh nobody asked for is
    /// not the place to find out a command does not exist.
    [<TestMethod>]
    member _.RefreshRefusesAnUnknownCommand() =
        let harness = journal ()

        match (harness.Refresh "wibble").Fault with
        | Some fault -> StringAssert.Contains(fault.Message, "only re-reads")
        | None -> Assert.Fail "A refresh of an unknown name should have been refused."

    // ------------------------------------------- a question about the row (0033)

    /// The static check is made where the binder builds the query, so `find` has it
    /// too: comparing two words is the same for every record.
    [<TestMethod>]
    member _.FindRefusesAPredicateThatNeverReadsTheRow() =
        let harness = journal ()
        let fault = harness.Fail "find mood eq great"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)

        Assert.AreEqual<string>("mood eq great never reads $row, so it is the same for every row.", fault.Message)

        Assert.AreEqual<string list>(
            [ "Did you mean $row.mood eq great?" ],
            fault.Notes |> List.map (fun note -> note.Text))

    /// `in` with an operator in it is a view, so it is checked, and the location is
    /// left where it was.
    [<TestMethod>]
    member _.InRefusesAPredicateThatNeverReadsTheRow() =
        let harness = journal ()
        harness.Run "in journal" |> ignore
        let fault = harness.Fail "in mood eq great"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)

        Assert.AreEqual<string>("mood eq great never reads $row, so it is the same for every row.", fault.Message)

        Assert.AreEqual<string list>(
            [ "Did you mean $row.mood eq great?" ],
            fault.Notes |> List.map (fun note -> note.Text))

        Assert.AreEqual<string>("/journal", harness.Location)
        Assert.AreEqual<Expr option>(None, harness.View)

    /// `in` with a plain operand is a path (decision 0013), and is not checked.
    [<TestMethod>]
    member _.InOnAPlainOperandIsNotChecked() =
        let harness = journal ()
        harness.Run "in journal" |> ignore

        Assert.AreEqual<string>("/journal", harness.Location)

    /// `save-view` has it too, and writes nothing.
    [<TestMethod>]
    member _.SaveViewRefusesAPredicateThatNeverReadsTheRow() =
        let harness = journal ()
        let fault = harness.Fail "save-view work tag eq work"

        Assert.AreEqual<FaultKind>(Binding, fault.Kind)

        Assert.AreEqual<string>("tag eq work never reads $row, so it is the same for every row.", fault.Message)

        Assert.AreEqual<string list>(
            [ "Did you mean $row.tag eq work?" ],
            fault.Notes |> List.map (fun note -> note.Text))

        Assert.IsFalse(harness.Exists "/work")

    /// A view lists by testing every record, so a record whose answer is a gap is
    /// skipped rather than stopping the listing.
    [<TestMethod>]
    member _.AViewSkipsARecordWithAGap() =
        let harness = journal ()

        Assert.AreEqual<string>("saturday stray", harness.Names "find $row.mood eq great and not $row.nothing")

/// <summary>`back`: to where you were before the last move (decision 0037).</summary>
/// <remarks>
/// `in` and `out` leave a trail of the places they left, folders and views alike, and
/// `back` retraces it one step at a time, like a browser's back button. The trail is a
/// fold over the log, so `undo` takes a `back` back, and a reload replays it.
/// </remarks>
[<TestClass>]
type BackTests() =

    /// A session over a log, standing for a tab; a second one over the same log is a reload.
    let open' (log: ILog) =
        let mutable counter = 100

        let options =
            { SessionOptions.defaults with
                Clock = (fun () -> System.DateTimeOffset(2026, 9, 21, 12, 0, 0, System.TimeSpan.Zero))
                NewId =
                    fun () ->
                        counter <- counter + 1
                        sprintf "id-%d" counter }

        let session = Session(log, options, SessionOptions.standardSeed options)
        Async.RunSynchronously(session.Initialize())
        session

    let display (session: Session) (line: string) =
        let response = Async.RunSynchronously(session.Execute line)

        match response.Fault with
        | Some fault -> raise (AssertFailedException(sprintf "'%s' failed: %s" line fault.Message))
        | None -> response.Result |> Option.map Value.display |> Option.defaultValue ""

    let spec (harness: Harness) (name: string) =
        harness.Session.Commands |> List.find (fun c -> c.Name = name)

    [<TestMethod>]
    member _.EachBackGoesOneStepFurther() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in /examples" |> ignore

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/documents", harness.Location)

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)

    /// It answers what `in` would have: the folder's record, a chip to tap and pipe.
    [<TestMethod>]
    member _.BackAnswersTheFolderItReturnedTo() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in /examples" |> ignore

        match harness.Run "back" with
        | Value.File file -> Assert.AreEqual<string>("documents", file.Name)
        | other -> Assert.Fail(sprintf "back answered %s, not a folder." (Value.kind other))

        Assert.AreEqual<Value>(Value.Text "/", harness.Run "back")

    [<TestMethod>]
    member _.BackFromAViewReturnsToTheFolder() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in $row.kind eq folder" |> ignore

        harness.Run "back" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)
        Assert.AreEqual<Expr option>(None, harness.View)

    /// A view is a place on the trail like a folder, so leaving one and going back
    /// asks the same question again.
    [<TestMethod>]
    member _.BackReturnsToAView() =
        let harness = seeded ()
        harness.Run "in $row.kind eq folder" |> ignore
        harness.Run "in documents" |> ignore

        match harness.Run "back" with
        | Value.Query expr -> Assert.AreEqual<string>("$row.kind eq folder", Expr.display expr)
        | other -> Assert.Fail(sprintf "back answered %s, not the view." (Value.kind other))

        Assert.AreEqual<string>("/", harness.Location)
        Assert.AreEqual<string>("$row.kind eq folder", harness.ViewText)

    /// `out` is a move like `in`: going back after it returns to where `out` left.
    [<TestMethod>]
    member _.BackAfterOutReturnsToWhereOutLeft() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "out" |> ignore

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/documents", harness.Location)

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)

    /// Putting a view down with `out` is a move too.
    [<TestMethod>]
    member _.BackAfterOutOfAViewPutsTheViewBack() =
        let harness = seeded ()
        harness.Run "in $row.kind eq folder" |> ignore
        harness.Run "out" |> ignore

        harness.Run "back" |> ignore

        Assert.AreEqual<string>("$row.kind eq folder", harness.ViewText)

    /// At the start of the trail it is not a fault: it says where you are, and changes
    /// nothing, so there is nothing for `undo` to take back.
    [<TestMethod>]
    member _.BackAtTheStartSaysThereIsNowhereFurtherBack() =
        let harness = seeded ()

        Assert.AreEqual<string>("Nowhere further back: you are in /", harness.Text "back")
        Assert.AreEqual<string>("/", harness.Location)
        Assert.AreEqual<string list>([ "seed" ], harness.History())
        Assert.AreEqual<string>("Nothing to undo.", harness.Text "undo")

    [<TestMethod>]
    member _.BackAtTheEndOfTheTrailSaysWhereYouAre() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "back" |> ignore

        Assert.AreEqual<string>("Nowhere further back: you are in /", harness.Text "back")

    /// A log written before the trail existed moved without leaving one, so a returning
    /// visitor starts with nowhere further back, and in a view that names the view.
    [<TestMethod>]
    member _.AMoveFromBeforeTheTrailLeavesNothingToGoBackTo() =
        let log = InMemoryLog()
        let first = open' log

        let view =
            match Expr.parse "test" "$row.kind eq folder" with
            | Ok expr -> expr
            | Error fault -> failwith fault.Message

        // What `in $row.kind eq folder` committed before Phase 9: the move alone.
        Async.RunSynchronously(
            first.Store.Commit
                "in $row.kind eq folder"
                [ LocationChanged({ Folder = "/"; View = None }, { Folder = "/"; View = Some view }) ])
        |> ignore

        let second = open' log

        Assert.AreEqual<string>("Nowhere further back: you are in $row.kind eq folder", display second "back")

    /// `back` is a line like any move: in `history`, and `undo` takes it back, which
    /// puts the place back on the trail as well.
    [<TestMethod>]
    member _.UndoTakesABackBack() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "back" |> ignore

        Assert.AreEqual<string>("Undone: back", harness.Text "undo")
        Assert.AreEqual<string>("/documents", harness.Location)

        // The place is on the trail again, so `back` goes there again.
        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)

    [<TestMethod>]
    member _.RedoGoesBackAgain() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in /examples" |> ignore
        harness.Run "back" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("Redone: back", harness.Text "redo")
        Assert.AreEqual<string>("/documents", harness.Location)

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)

    /// Undoing a move takes back the place it left too, so `back` does not return to
    /// somewhere the undone move never left.
    [<TestMethod>]
    member _.UndoingAMoveTakesItsPlaceOffTheTrail() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in /examples" |> ignore
        harness.Run "undo" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)
        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)
        Assert.AreEqual<string>("Nowhere further back: you are in /", harness.Text "back")

    [<TestMethod>]
    member _.BackIsInHistory() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "back" |> ignore

        Assert.AreEqual<string list>([ "seed"; "in documents"; "back" ], harness.History())

    /// A move that goes nowhere leaves nothing on the trail: `in` where you already
    /// are commits nothing.
    [<TestMethod>]
    member _.AMoveToWhereYouAreLeavesNoStep() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "in ." |> ignore

        harness.Run "back" |> ignore
        Assert.AreEqual<string>("/", harness.Location)

    /// A folder deleted since is passed over: a `back` that failed on it would fail
    /// every time, because nothing else takes a place off the trail.
    [<TestMethod>]
    member _.BackPassesOverAFolderThatIsGone() =
        let harness = seeded ()
        harness.Run "in documents" |> ignore
        harness.Run "mkdir sub" |> ignore
        harness.Run "in sub" |> ignore
        harness.Run "in /" |> ignore
        harness.Run "rm documents/sub" |> ignore

        harness.Run "back" |> ignore

        Assert.AreEqual<string>("/documents", harness.Location)

    /// The trail is in the log, so a reload keeps it, and `undo` of a `back` still
    /// works after one.
    [<TestMethod>]
    member _.BackWorksAcrossAReload() =
        let log = InMemoryLog()
        let first = open' log
        display first "in documents" |> ignore
        display first "in /examples" |> ignore

        let second = open' log
        display second "back" |> ignore
        Assert.AreEqual<string>("/documents", second.Location.Folder)

        let third = open' log
        Assert.AreEqual<string>("/documents", third.Location.Folder)
        Assert.AreEqual<string>("Undone: back", display third "undo")
        Assert.AreEqual<string>("/examples", third.Location.Folder)
        display third "back" |> ignore
        display third "back" |> ignore
        Assert.AreEqual<string>("/", third.Location.Folder)
        Assert.AreEqual<string>("Nowhere further back: you are in /", display third "back")

    /// A view on the trail survives a reload, stored as its text and read back.
    [<TestMethod>]
    member _.AViewOnTheTrailSurvivesAReload() =
        let log = InMemoryLog()
        let first = open' log
        display first "in $row.kind eq folder" |> ignore
        display first "in documents" |> ignore

        let second = open' log
        display second "back" |> ignore

        Assert.AreEqual<string>(
            "$row.kind eq folder",
            second.Location.View |> Option.map Expr.display |> Option.defaultValue "")

    /// `back` changes where you are, so it is not read-only: a live view must not re-run it.
    [<TestMethod>]
    member _.BackIsNotReadOnly() =
        Assert.IsFalse((spec (seeded ()) "back").ReadOnly)

    /// `back` came from `up`'s keywords; it is a command of its own now, so `out` no
    /// longer answers to it.
    [<TestMethod>]
    member _.OutNoLongerAnswersToBack() =
        let harness = seeded ()

        Assert.IsFalse(List.contains "back" (spec harness "out").Keywords)
        Assert.IsTrue(List.contains "up" (spec harness "out").Keywords)

    /// Leaving a place pushes it; undoing that pops it; both are replayed.
    [<TestMethod>]
    member _.TheTrailIsAFoldThatUndoInverts() =
        let documents = { Folder = "/documents"; View = None }
        let pushed = Projection.apply Projection.empty (TrailPushed documents)

        Assert.AreEqual<Location list>([ documents ], pushed.Trail)
        Assert.AreEqual<Location list>([], (Projection.apply pushed (Projection.invert (TrailPushed documents))).Trail)
        Assert.AreEqual<Event>(TrailPushed documents, Projection.invert (Projection.invert (TrailPushed documents)))

    /// Forgiving, like the rest of the fold: taking off a place that is not there
    /// changes nothing rather than failing a replay.
    [<TestMethod>]
    member _.PoppingAPlaceThatIsNotThereChangesNothing() =
        let documents = { Folder = "/documents"; View = None }
        let projection = Projection.apply Projection.empty (TrailPushed documents)

        let popped = Projection.apply projection (TrailPopped { Folder = "/elsewhere"; View = None })

        Assert.AreEqual<Location list>([ documents ], popped.Trail)
