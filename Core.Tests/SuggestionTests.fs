namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core

/// <summary>A fix becomes the whole corrected line (decision 0044).</summary>
/// <remarks>
/// Phase 10's foundation: whatever finds a mistake says its fix as a replacement of
/// what was written, and the session makes it a line against what the person typed.
/// Stream A's suggestions and fixes are tested in the classes after this one.
/// </remarks>
[<TestClass>]
type FixTests() =

    static let apply source fix = Fix.apply source fix

    [<TestMethod>]
    member _.AReplacementIsMadeWhereTheLineHasTheWord() =
        Assert.AreEqual<string option>(Some "ls -a", apply "lss -a" (Fix.Replace("lss", "ls")))
        Assert.AreEqual<string option>(Some "echo $files", apply "echo $fles" (Fix.Replace("$fles", "$files")))

        Assert.AreEqual<string option>(
            Some "ls | where $row.kind eq folder",
            apply "ls | where kind eq folder" (Fix.Replace("kind eq folder", "$row.kind eq folder"))
        )

    /// A word inside a longer one, or read off a variable, is not the word written.
    [<TestMethod>]
    member _.AReplacementTakesOnlyAWholeWord() =
        Assert.AreEqual<string option>(
            Some "ls | where $row.kinds eq kind",
            apply "ls | where $row.kinds eq knd" (Fix.Replace("knd", "kind"))
        )

        Assert.AreEqual<string option>(
            Some "ls | where $row.kind eq knd",
            apply "ls | where $row.knd eq knd" (Fix.Replace("$row.knd", "$row.kind"))
        )

        Assert.AreEqual<string option>(None, apply "ls | where $row.kinds eq folder" (Fix.Replace("kind", "knd")))

    /// A fix the line has no place for, or that changes nothing, is not offered.
    [<TestMethod>]
    member _.AFixThatMakesNoNewLineIsDropped() =
        Assert.AreEqual<string option>(None, apply "run script.clr" (Fix.Replace("lss", "ls")))
        Assert.AreEqual<string option>(None, apply "ls" (Fix.Line "ls"))
        Assert.AreEqual<string option>(None, apply "ls" (Fix.Replace("", "x")))
        Assert.AreEqual<string option>(Some "ls", apply "lss" (Fix.Line "ls"))

    [<TestMethod>]
    member _.ANoteResolvesToDistinctLines() =
        let note =
            Note.suggestion
                "Did you mean ls?"
                [ Fix.Replace("lss", "ls"); Fix.Line "ls"; Fix.Replace("nowhere", "x") ]

        let resolved = Note.resolve "lss" note

        Assert.AreEqual<string>("suggestion", resolved.Kind)
        Assert.AreEqual<string>("Did you mean ls?", resolved.Text)
        Assert.AreEqual<string list>([ "ls" ], Note.fixLines resolved)

    /// A fault that becomes a value leaves its notes behind (decision 0041).
    [<TestMethod>]
    member _.AFaultAsAValueHasNoNotes() =
        let note = Note.suggestion "Did you mean ls?" [ Fix.Line "ls" ]
        let inner = Fault.create Invalid "inner" |> Fault.withNotes [ note ]
        let fault = Fault.create NotFound "outer" |> Fault.withNotes [ note ] |> Fault.causedBy inner
        let value = Fault.asValue fault

        Assert.AreEqual<Note list>([], value.Notes)
        Assert.AreEqual<Note list>([], value.Cause.Value.Notes)
        Assert.AreEqual<string>("outer", value.Message)
