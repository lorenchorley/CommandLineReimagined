namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core
open CommandLineReimagined.Core.Tests.SessionHarness

/// <summary>Where the cursor is: the contract every Phase 8 stream codes against.</summary>
/// <remarks>
/// One row per line of the Phase 8 finding table, and the lines the lexical rules were
/// already right about, each with the place `Context.analyse` must read it as. A place
/// is compared as `Context.describe` writes it, so a stream that adds a field to a
/// parameter does not break a row. `‸` marks the cursor where it is not at the end.
/// </remarks>
[<TestClass>]
type ContextTests() =

    static let specs = lazy ((Harness()).Session.Commands)

    static let analyse (line: string) =
        let cursor = line.IndexOf '‸'
        let text = line.Replace("‸", "")
        Context.analyse specs.Value text (if cursor < 0 then text.Length else cursor)

    static let place (line: string) = Context.describe (analyse line).Place

    static let rows =
        [ // 1, 2: a variable being named
          "$", "Variable"
          "echo $", "Variable"
          "echo $r", "Variable"
          "ls | where $", "Variable inPredicate"
          "ls | where $r", "Variable inPredicate"
          "ls | where $row.kind eq $", "Variable inPredicate"
          // 3, 4: run alone, and outside a predicate
          "$v", "Variable"
          "$row", "Variable"
          "echo $row", "Variable"
          // 5: members of what a variable holds
          "$v.", "Member $v"
          "$problem.", "Member $problem"
          "$files.", "Member $files"
          "echo $problem.ki", "Member $problem in echo"
          // 6, 25, 27: $row's members, in the stage whose input they are
          "ls documents | where $row.", "Member $row in where"
          "ls | select name | where $row.", "Member $row in where"
          "ls | where $row.", "Member $row in where"
          "ls | where $row.kind", "Member $row in where"
          // 7, 8, 9: command names
          "", "Blank"
          "  ", "Blank"
          "wh", "CommandName"
          "ls | ", "CommandName afterPipe"
          "ls | se", "CommandName afterPipe"
          "lss", "CommandName"
          "delete", "CommandName"
          "try c", "CommandName"
          "ls | try c", "CommandName afterPipe"
          "cat x else c", "CommandName"
          "first (c", "CommandName"
          "echo (ls | c", "CommandName afterPipe"
          "ls | where $row.size gt (ls | c", "CommandName afterPipe"
          // 10, 11: help takes a command's name
          "help where", "Argument(help, command)"
          "help ", "Argument(help, command)"
          // 12, 13, 19: arguments, by the parameter they bind to
          "ls | sort ", "Argument(sort, column)"
          "ls | sort name", "Argument(sort, column)"
          "ls | sort name d", "Argument(sort, desc)"
          "select ", "Argument(select, columns)"
          "ls | select name ", "Argument(select, columns)"
          "group ", "Argument(group, column)"
          "distinct ", "Argument(distinct, column)"
          // 14, 15: nothing to pick, and a name to replace
          "take ", "Argument(take, count)"
          "ls | skip ", "Argument(skip, count)"
          "progress ", "Argument(progress, steps)"
          "progress 5 ", "Argument(progress, delay)"
          "set ", "Argument(set, name)"
          "set v ", "Argument(set, value)"
          // 16: a predicate starts
          "where ", "Predicate(where, Operand)"
          "ls | where ", "Predicate(where, Operand)"
          "find ", "Predicate(find, Operand)"
          "save-view x ", "Predicate(save-view, Operand)"
          "cd doc", "Predicate(cd, Operand)"
          "ls | where not ", "Predicate(where, Operand)"
          "ls | where $row.kind eq folder and ", "Predicate(where, Operand)"
          // 17, 18: assignments and flags
          "attr readme.txt ", "Argument(attr, assignment)"
          "attr readme.txt mood=go", "Argument(attr, mood=)"
          "attr ", "Argument(attr, path)"
          "ls | sort name -", "Argument(sort, flag)"
          "ls | sort name -de", "Argument(sort, flag)"
          // 20: a quoted word, and paths
          "cat \"doc", "Argument(cat, path)"
          "cat re", "Argument(cat, path)"
          "cat documents/no", "Argument(cat, path)"
          "cd ", "Predicate(cd, Operand)"
          "cp readme.txt ", "Argument(cp, targetPath)"
          "cat x el", "Argument(cat, surplus)"
          "frob x", "Argument(frob, surplus)"
          // 21: tags
          "save <", "TagType"
          "save <no", "TagType"
          "save <note ", "TagAttribute note"
          "save <note mood=good ", "TagAttribute note"
          // 22, 23, 24, 26, 28: inside a predicate
          "ls | where $row.kind ", "Predicate(where, AfterOperand $row.kind)"
          "ls | where $row.kind e", "Predicate(where, AfterOperand $row.kind)"
          "ls | where $row.kind eq", "Predicate(where, AfterOperand $row.kind)"
          "ls | where $row.kind eq ", "Predicate(where, ComparisonRight $row.kind eq)"
          "ls | where $row.kind eq f", "Predicate(where, ComparisonRight $row.kind eq)"
          "ls | where kind eq folder", "Predicate(where, ComparisonRight kind eq)"
          "ls | where $row.kind eq folder ", "Predicate(where, AfterComparison)"
          "ls | where $row.kind eq folder a", "Predicate(where, AfterComparison)"
          "ls | where not $row.done ", "Predicate(where, AfterOperand $row.done)"
          "ls | where $row.a eq b and $row.c ", "Predicate(where, AfterOperand $row.c)"
          // 32: the cursor in the middle of a line
          "cat re‸ documents", "Argument(cat, path)"
          "ls | sort ‸ | take 2", "Argument(sort, column)"
          "ls‸ | sort name", "CommandName" ]

    [<TestMethod>]
    member _.EveryLineReadsAsItsPlace() =
        let wrong =
            rows
            |> List.choose (fun (line, expected) ->
                let actual = place line
                if actual = expected then None else Some(sprintf "%-40s expected %s, read %s" line expected actual))

        if not (List.isEmpty wrong) then
            Assert.Fail("\n" + String.concat "\n" wrong)

    /// The stages before the cursor, written out so `Shape` can run them.
    [<TestMethod>]
    member _.AStageKnowsWhatComesBeforeIt() =
        let upstream line =
            match (analyse line).Place with
            | Place.Member(_, _, Some stage) -> stage.Upstream
            | Place.Argument(stage, _)
            | Place.Predicate(stage, _) -> stage.Upstream
            | other -> Assert.Fail(sprintf "%s is not in a stage: %A" line other); None

        Assert.AreEqual<string option>(Some "ls documents", upstream "ls documents | where $row.")
        Assert.AreEqual<string option>(Some "ls | select name", upstream "ls | select name | where $row.")
        Assert.AreEqual<string option>(None, upstream "sort ")
        Assert.AreEqual<string option>(Some "ls", upstream "cat x else ls | sort ")

    [<TestMethod>]
    member _.AStageKnowsWhatIsWrittenAlready() =
        match (analyse "ls | sort name ").Place with
        | Place.Argument(stage, _) ->
            Assert.AreEqual<int>(1, stage.Index)
            Assert.AreEqual<int>(1, stage.Written.Length)
            Assert.AreEqual<string>("sort", stage.Spec.Value.Name)
        | other -> Assert.Fail(sprintf "%A" other)

    /// A completion replaces the word, from its start to its end, so the rest of the
    /// line stays where it is.
    [<TestMethod>]
    member _.TheWordRunsToItsEnd() =
        let word line = (analyse line).Word

        Assert.AreEqual<Word>({ Start = 4; End = 6; Prefix = "re"; Quoted = false }, word "cat re‸ documents")
        Assert.AreEqual<Word>({ Start = 4; End = 14; Prefix = "re"; Quoted = false }, word "cat re‸adme.txt")
        Assert.AreEqual<Word>({ Start = 4; End = 8; Prefix = "doc"; Quoted = true }, word "cat \"doc")
        Assert.AreEqual<Word>({ Start = 11; End = 18; Prefix = "$row.ki"; Quoted = false }, word "ls | where $row.ki")
        Assert.AreEqual<Word>({ Start = 5; End = 5; Prefix = ""; Quoted = false }, word "ls | ")
