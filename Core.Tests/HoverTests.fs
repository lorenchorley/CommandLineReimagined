namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// What a tapped token is (stream G).
///
/// The page asks with the offset at the end of the token, so each line here is written
/// with `‸` where the page would put it.
[<TestClass>]
type HoverTests() =

    let at (line: string) =
        let offset = line.IndexOf '‸'
        line.Replace("‸", ""), offset

    let describe (harness: Harness) (line: string) =
        let text, offset = at line
        Async.RunSynchronously(harness.Session.Describe(text, offset))

    let expect (hover: Hover option) =
        match hover with
        | Some hover -> hover
        | None -> raise (AssertFailedException "Expected something to say about the token, and there was nothing.")

    /// The smoke test the foundation leaves: asking never fails.
    [<TestMethod>]
    member _.AskingAboutATokenNeverFails() =
        let harness = seeded ()

        Async.RunSynchronously(harness.Session.Describe("ls | sort name", 6)) |> ignore

    [<TestMethod>]
    member _.AVariableSaysWhatItHolds() =
        let harness = seeded ()
        harness.Run "set v 5" |> ignore
        harness.Run "ls | set files" |> ignore

        let number = expect (describe harness "echo $v‸")
        Assert.AreEqual<string>("variable", number.Kind)
        Assert.AreEqual<string>("$v", number.Text)
        Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Number 5.0)), number.Detail)

        // The tokens keep `$` apart from the name, and a tap on either is the variable.
        let sigil = expect (describe harness "echo $‸v")
        Assert.AreEqual<string>("$v", sigil.Text)
        Assert.AreEqual<string option>(number.Detail, sigil.Detail)

        // The same words `Summary` gives, whatever it gives, so hover and the chips agree.
        let table = expect (describe harness "$files‸ | count")
        Assert.AreEqual<string option>(Some(Summary.ofValue (harness.Variable "files").Value), table.Detail)

    [<TestMethod>]
    member _.AVariableThatIsNotSetSaysSo() =
        let hover = expect (describe (seeded ()) "echo $nope‸")

        Assert.AreEqual<string option>(Some "not set", hover.Detail)

    [<TestMethod>]
    member _.RowIsTheRowBeingTestedInsideAPredicate() =
        let harness = seeded ()

        let inside = expect (describe harness "ls | where $row‸.kind eq folder")
        Assert.AreEqual<string>("variable", inside.Kind)
        Assert.AreEqual<string option>(Some "the row being tested", inside.Detail)

        let outside = expect (describe harness "echo $row‸")
        StringAssert.Contains(outside.Detail.Value, "only inside where")

    [<TestMethod>]
    member _.ACommandShowsItsSignatureAndDescription() =
        let harness = seeded ()
        let hover = expect (describe harness "ls | sort‸ name")
        let spec = harness.Session.Commands |> List.find (fun spec -> spec.Name = "sort")

        Assert.AreEqual<string>("command", hover.Kind)
        Assert.AreEqual<string>("sort", hover.Text)
        Assert.AreEqual<string option>(Some spec.Description, hover.Detail)

        let signature = hover.Signature.Value
        Assert.AreEqual<string>("sort", signature.Command)
        Assert.AreEqual<string>(spec.Description, signature.Description)

        CollectionAssert.AreEqual(
            spec.Parameters |> List.map (fun p -> p.Name) |> Array.ofList,
            signature.Parameters |> List.map (fun (name, _, _) -> name) |> Array.ofList)

        Assert.AreEqual<int option>(None, signature.Active)

    [<TestMethod>]
    member _.ANameThatIsNotACommandHasNothingToSay() =
        Assert.AreEqual<Hover option>(None, describe (seeded ()) "lss‸")

    [<TestMethod>]
    member _.ARowMemberShowsItsColumnType() =
        let harness = seeded ()

        let size = expect (describe harness "ls | where $row.size‸ gt 10")
        Assert.AreEqual<string>("member", size.Kind)
        Assert.AreEqual<string>("$row.size", size.Text)
        Assert.AreEqual<string option>(Some "column · number", size.Detail)

        let kind = expect (describe harness "ls | where $row.kind‸ eq folder")
        Assert.AreEqual<string option>(Some "column · text", kind.Detail)

    [<TestMethod>]
    member _.AMemberOfAHeldTableShowsItsColumnType() =
        let harness = seeded ()
        harness.Run "ls | set files" |> ignore

        let hover = expect (describe harness "echo $files.size‸")

        Assert.AreEqual<string option>(Some "column · number", hover.Detail)

    [<TestMethod>]
    member _.AMemberOfAFaultShowsWhatItReads() =
        let harness = seeded ()
        harness.Run "try read missing.txt | set problem" |> ignore

        let hover = expect (describe harness "echo $problem.kind‸")

        Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Text "NotFound")), hover.Detail)

    /// Decision 0048: a tag's own parts say what they read, then what that is.
    [<TestMethod>]
    member _.ATagsOwnPartsSayWhatTheyRead() =
        let harness = seeded ()
        harness.Run "set v <thing a=1><b/><c/></thing>" |> ignore

        let tag = expect (describe harness "echo $v.@tag‸")
        Assert.AreEqual<string>("member", tag.Kind)
        Assert.AreEqual<string>("$v.@tag", tag.Text)
        Assert.AreEqual<string option>(Some "the tag's name · text · \"thing\"", tag.Detail)

        let children = expect (describe harness "echo $v.@children‸")
        Assert.AreEqual<string option>(Some "its children · list · 2 items", children.Detail)

        let attribute = expect (describe harness "echo $v.a‸")
        Assert.AreEqual<string option>(Some "number · 1", attribute.Detail)

    /// A row's own `@tag` column is only a column (decision 0050), so hover says what it
    /// holds, as completion does, and not that it is the tag's name.
    [<TestMethod>]
    member _.ARowsAtColumnIsDescribedAsAColumn() =
        let harness = seeded ()
        harness.Run "set d <library><book title=dune/></library>" |> ignore
        harness.Run "$d | pick book | first | set b" |> ignore

        let tag = expect (describe harness "echo $b.@tag‸")
        Assert.AreEqual<string option>(Some(Summary.ofValue (Value.Text "book")), tag.Detail)

    /// Any other `@` name reads nothing, so there is nothing to say about what it holds.
    [<TestMethod>]
    member _.AnyOtherAtMemberSaysNothingOfWhatItHolds() =
        let harness = seeded ()
        harness.Run "set v <thing a=1/>" |> ignore

        let other = expect (describe harness "echo $v.@other‸")
        Assert.AreEqual<string option>(None, other.Detail)

    [<TestMethod>]
    member _.AnOperatorSaysWhatItCompares() =
        let harness = seeded ()

        let eq = expect (describe harness "ls | where $row.kind eq‸ folder")
        Assert.AreEqual<string>("operator", eq.Kind)
        Assert.AreEqual<string option>(Some "true when $row.kind is equal to folder", eq.Detail)

        let gt = expect (describe harness "ls | where $row.size gt‸ 10")
        Assert.AreEqual<string option>(Some "true when $row.size is greater than 10", gt.Detail)

        let both = expect (describe harness "ls | where $row.size gt 10 and‸ $row.kind eq file")
        Assert.AreEqual<string>("operator", both.Kind)
        Assert.AreEqual<string option>(Some "true when both sides are true", both.Detail)

    [<TestMethod>]
    member _.AnArgumentShowsTheParameterItBindsTo() =
        let harness = seeded ()
        let hover = expect (describe harness "ls | sort name‸")
        let signature = hover.Signature.Value

        Assert.AreEqual<string>("sort", signature.Command)
        let active = signature.Active.Value
        let name, _, description = signature.Parameters[active]
        Assert.AreEqual<string>(name + " · " + description, hover.Detail.Value)

    [<TestMethod>]
    member _.AFlagShowsTheParameterItSets() =
        let harness = seeded ()
        let hover = expect (describe harness "ls | sort name -desc‸")

        Assert.AreEqual<string>("flag", hover.Kind)
        StringAssert.StartsWith(hover.Detail.Value, "desc · ")
