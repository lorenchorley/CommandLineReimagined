using System.Text.Json.Nodes;
using CommandLineReimagined.Core;
using CommandLineReimagined.Web.Persistence;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace Web.Core.Tests;

/// <summary>
/// The on-disk shape of a transaction.
/// </summary>
/// <remarks>
/// What is written here is read back by a later build, so these are not testing a
/// serialiser: they are testing a contract. Every event case and every value case has
/// a round trip, so adding one without teaching the format about it fails here rather
/// than silently making somebody's filesystem unreadable.
/// </remarks>
[TestClass]
public class LogFormatTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 34, 56, 789, TimeSpan.FromHours(2));

    /// <summary>
    /// A tag's attributes as an ordered list, which is how a tag is built.
    /// </summary>
    /// <remarks>
    /// A tag keeps the order it was written in beside the map, so building one from a
    /// map alone would lose exactly what the order field is there to keep.
    /// </remarks>
    private static FSharpList<Tuple<string, Value>> Pairs(params (string Name, Value Value)[] pairs) =>
        ListModule.OfSeq(pairs.Select(pair => Tuple.Create(pair.Name, pair.Value)));

    private static FSharpMap<string, Value> Attributes(params (string Name, Value Value)[] pairs) =>
        MapModule.OfSeq(pairs.Select(p => Tuple.Create(p.Name, p.Value)));

    private static FileRecord Record(string id = "id-1") =>
        new(id,
            Attributes(
                ("name", Value.NewText("notes.txt")),
                ("kind", Value.NewText("text")),
                ("folder", Value.NewText("/documents"))),
            FSharpOption<string>.Some("abc123"));

    private static Transaction Transaction(params Event[] events) =>
        new(7L, At, "write notes.txt hello", ListModule.OfSeq(events), FSharpOption<long>.None, true);

    /// Writes, reads back, writes again, and compares the text. Comparing the JSON
    /// rather than the objects means a field that is dropped on the way in and
    /// defaulted on the way out cannot pass.
    private static Transaction RoundTrip(Transaction transaction)
    {
        string first = LogFormat.Write(transaction);
        var read = LogFormat.Read(first);
        string second = LogFormat.Write(read);

        Assert.AreEqual(first, second, "The shape did not survive a round trip.");
        return read;
    }

    // ---- events -----------------------------------------------------------------

    [TestMethod]
    public void FileCreatedRoundTrips()
    {
        var read = RoundTrip(Transaction(Event.NewFileCreated(Record())));

        var created = (Event.FileCreated)read.Events.Single();
        Assert.AreEqual("id-1", created.Item.Id);
        Assert.AreEqual("notes.txt", Attributes_.Text(created.Item, "name"));
        Assert.AreEqual("abc123", created.Item.Content!.Value);
    }

    [TestMethod]
    public void FileDeletedRoundTrips()
    {
        var read = RoundTrip(Transaction(Event.NewFileDeleted(Record())));

        Assert.IsInstanceOfType(read.Events.Single(), typeof(Event.FileDeleted));
    }

    [TestMethod]
    public void AttributesChangedRoundTrips()
    {
        var before = Attributes(("tag", Value.NewText("draft")));
        var after = Attributes(("tag", Value.NewText("work")), ("due", Value.NewText("2026-10-01")));

        var read = RoundTrip(Transaction(Event.NewAttributesChanged("id-1", before, after)));

        var changed = (Event.AttributesChanged)read.Events.Single();
        Assert.AreEqual("draft", ValueModule.display(changed.before["tag"]));
        Assert.AreEqual("2026-10-01", ValueModule.display(changed.after["due"]));
    }

    [TestMethod]
    public void ContentChangedRoundTrips()
    {
        var read = RoundTrip(Transaction(Event.NewContentChanged(
            "id-1", FSharpOption<string>.Some("old"), FSharpOption<string>.Some("new"))));

        var changed = (Event.ContentChanged)read.Events.Single();
        Assert.AreEqual("old", changed.before!.Value);
        Assert.AreEqual("new", changed.after!.Value);
    }

    /// A file that did not exist before has no previous hash, and that absence is not
    /// the empty string.
    [TestMethod]
    public void ContentChangedKeepsAnAbsentHashAbsent()
    {
        var read = RoundTrip(Transaction(Event.NewContentChanged(
            "id-1", FSharpOption<string>.None, FSharpOption<string>.Some("new"))));

        var changed = (Event.ContentChanged)read.Events.Single();
        Assert.IsNull(changed.before);
        Assert.AreEqual("new", changed.after!.Value);
    }

    [TestMethod]
    public void VariableChangedRoundTrips()
    {
        var read = RoundTrip(Transaction(Event.NewVariableChanged(
            "v", FSharpOption<Value>.Some(Value.NewNumber(1)), FSharpOption<Value>.Some(Value.NewNumber(2)))));

        var changed = (Event.VariableChanged)read.Events.Single();
        Assert.AreEqual("1", ValueModule.display(changed.before!.Value));
        Assert.AreEqual("2", ValueModule.display(changed.after!.Value));
    }

    /// Unbinding is an absent value, and reading it back as one is what makes undo of
    /// a `set` restore nothing rather than something empty.
    [TestMethod]
    public void VariableChangedKeepsAnUnboundSideAbsent()
    {
        var read = RoundTrip(Transaction(Event.NewVariableChanged(
            "v", FSharpOption<Value>.Some(Value.NewNumber(1)), FSharpOption<Value>.None)));

        var changed = (Event.VariableChanged)read.Events.Single();
        Assert.IsNotNull(changed.before);
        Assert.IsNull(changed.after);
    }

    [TestMethod]
    public void LocationChangedRoundTrips()
    {
        var read = RoundTrip(Transaction(Event.NewLocationChanged(
            new Location("/", FSharpOption<Expr>.None),
            new Location("/documents", FSharpOption<Expr>.None))));

        var changed = (Event.LocationChanged)read.Events.Single();
        Assert.AreEqual("/", changed.before.Folder);
        Assert.AreEqual("/documents", changed.after.Folder);
    }

    /// <summary>
    /// A view survives the round trip, which is what makes undo work across a reload.
    /// </summary>
    /// <remarks>
    /// It is stored as the predicate's text and read back through the grammar, so a
    /// location that was entered as a question comes back as the same question rather
    /// than as the folder it happened to be over.
    /// </remarks>
    [TestMethod]
    public void LocationChangedKeepsItsView()
    {
        var view = ExprModule.parse("test", "$row.kind eq folder");

        var read = RoundTrip(Transaction(Event.NewLocationChanged(
            new Location("/", FSharpOption<Expr>.None),
            new Location("/", FSharpOption<Expr>.Some(view.ResultValue)))));

        var changed = (Event.LocationChanged)read.Events.Single();

        Assert.IsNull(changed.before.View);
        Assert.IsNotNull(changed.after.View);
        Assert.AreEqual("$row.kind eq folder", ExprModule.display(changed.after.View!.Value));
    }

    // ---- transactions -----------------------------------------------------------

    [TestMethod]
    public void ATransactionKeepsItsIdentity()
    {
        var read = RoundTrip(Transaction(Event.NewFileCreated(Record())));

        Assert.AreEqual(7L, read.Seq);
        Assert.AreEqual("write notes.txt hello", read.Source);
        Assert.IsTrue(read.Undoable);
        Assert.IsNull(read.Compensates);
    }

    /// The instant, not the nearest second: two transactions in the same second must
    /// still be distinguishable when read back.
    [TestMethod]
    public void ATimestampKeepsItsPrecisionAndOffset()
    {
        var read = RoundTrip(Transaction(Event.NewFileCreated(Record())));

        Assert.AreEqual(At, read.At);
        Assert.AreEqual(At.Offset, read.At.Offset);
    }

    [TestMethod]
    public void ACompensationKeepsWhatItCompensates()
    {
        var transaction = new Transaction(
            8L, At, "mkdir alpha",
            ListModule.OfSeq(new[] { Event.NewFileDeleted(Record()) }),
            FSharpOption<long>.Some(7L),
            true);

        var read = RoundTrip(transaction);

        Assert.AreEqual(7L, read.Compensates!.Value);
    }

    /// Decision 0018: the seed is recorded and is not the user's to take back, so the
    /// flag has to survive a reload or the first undo after one would empty it.
    [TestMethod]
    public void ATransactionThatIsNotUndoableStaysThatWay()
    {
        var transaction = new Transaction(
            1L, At, "seed",
            ListModule.OfSeq(new[] { Event.NewFileCreated(Record()) }),
            FSharpOption<long>.None,
            false);

        Assert.IsFalse(RoundTrip(transaction).Undoable);
    }

    [TestMethod]
    public void SeveralEventsKeepTheirOrder()
    {
        var read = RoundTrip(Transaction(
            Event.NewFileCreated(Record("a")),
            Event.NewContentChanged("a", FSharpOption<string>.None, FSharpOption<string>.Some("h")),
            Event.NewFileCreated(Record("b"))));

        Assert.AreEqual(3, read.Events.Length);
        Assert.AreEqual("a", ((Event.FileCreated)read.Events[0]).Item.Id);
        Assert.AreEqual("b", ((Event.FileCreated)read.Events[2]).Item.Id);
    }

    // ---- values -----------------------------------------------------------------

    /// <summary>Every value kind survives as the kind it was.</summary>
    /// <remarks>
    /// The reason values are written tagged rather than as the nearest JSON type. An
    /// attribute whose text is `2026` has to come back as text, and JSON cannot tell
    /// that from a number without being told.
    /// </remarks>
    [TestMethod]
    public void EveryValueKindRoundTripsAsItself()
    {
        var values = new (string Name, Value Value)[]
        {
            ("empty", Value.Empty),
            ("none", Value.None),
            ("text", Value.NewText("hello")),
            ("numericText", Value.NewText("2026")),
            ("number", Value.NewNumber(42.5)),
            ("negative", Value.NewNumber(-5)),
            ("boolean", Value.NewBoolean(true)),
            ("file", Value.NewFile(new FileRef("id", "notes.txt", "text", "/documents"))),
            ("list", Value.NewList(ListModule.OfSeq(new[] { Value.NewText("a"), Value.NewNumber(1) }))),
            ("object", Value.NewObject(TagModule.create("measurement",
                Pairs(("unit", Value.NewText("metres"))),
                ListModule.Empty<Value>()))),
            ("component", Value.NewComponent(TagModule.create("renderer",
                Pairs(("colour", Value.NewText("red"))),
                ListModule.Empty<Value>()))),
            // A listing bound to a variable: `ls | set files` puts one of these in the
            // log, so it has to come back as a table rather than as its text.
            ("table", Value.NewTable(new Table(
                ListModule.OfSeq(new[]
                {
                    new Column("name", ColumnType.FileCol),
                    new Column("size", ColumnType.NumberCol),
                }),
                ListModule.OfSeq(new[]
                {
                    ListModule.OfSeq(new[]
                    {
                        Value.NewFile(new FileRef("id", "notes.txt", "text", "/")),
                        Value.NewNumber(12),
                    }),
                })))),
        };

        var read = RoundTrip(Transaction(Event.NewAttributesChanged(
            "id-1", MapModule.Empty<string, Value>(), Attributes(values))));

        var after = ((Event.AttributesChanged)read.Events.Single()).after;

        foreach (var (name, expected) in values)
        {
            Assert.AreEqual(
                ValueModule.kind(expected), ValueModule.kind(after[name]), $"'{name}' came back as the wrong kind.");
            Assert.AreEqual(
                ValueModule.display(expected), ValueModule.display(after[name]), $"'{name}' came back reading differently.");
        }
    }

    /// <summary>A fault that <c>try</c> caught round-trips with everything a script can read off it.</summary>
    /// <remarks>
    /// Phase 5: <c>try cat nowhere.txt | set problem</c> puts a fault in a variable, and a
    /// variable is in the log, so a reload has to bring back <c>$problem.kind</c> as well
    /// as its message.
    /// </remarks>
    [TestMethod]
    public void AFaultValueRoundTrips()
    {
        var cause = new Fault(FaultKind.Invalid, "inner", FSharpOption<int>.None,
            FSharpOption<string>.None, FSharpOption<Fault>.None);
        var fault = new Fault(FaultKind.NotFound, "File does not exist : /nowhere.txt",
            FSharpOption<int>.Some(1), FSharpOption<string>.Some("/nowhere.txt"), FSharpOption<Fault>.Some(cause));

        var read = RoundTrip(Transaction(Event.NewVariableChanged(
            "problem", FSharpOption<Value>.None, FSharpOption<Value>.Some(Value.NewFault(fault)))));

        var after = ((Event.VariableChanged)read.Events.Single()).after!.Value;

        Assert.AreEqual(fault, ((Value.Fault)after).Item);
        Assert.AreEqual("File does not exist : /nowhere.txt", ValueModule.display(after));
    }

    /// <summary>A predicate held in a variable round-trips as the predicate it was.</summary>
    [TestMethod]
    public void AQueryValueRoundTrips()
    {
        var expr = ((Value.Query)Value.NewQuery(ExprModule.parse("test", "$row.kind eq note").ResultValue)).Item;

        var read = RoundTrip(Transaction(Event.NewVariableChanged(
            "here", FSharpOption<Value>.None, FSharpOption<Value>.Some(Value.NewQuery(expr)))));

        var after = ((Event.VariableChanged)read.Events.Single()).after!.Value;

        Assert.AreEqual("query", ValueModule.kind(after));
        Assert.AreEqual("$row.kind eq note", ValueModule.display(after));
    }

    [TestMethod]
    public void ANestedTagRoundTrips()
    {
        var inner = Value.NewObject(TagModule.create("inner",
            Pairs(("depth", Value.NewNumber(2))), ListModule.Empty<Value>()));

        var outer = Value.NewObject(TagModule.create("outer",
            Pairs(), ListModule.OfSeq(new[] { inner })));

        var read = RoundTrip(Transaction(Event.NewVariableChanged(
            "v", FSharpOption<Value>.None, FSharpOption<Value>.Some(outer))));

        var changed = (Event.VariableChanged)read.Events.Single();
        Assert.AreEqual("<outer><inner depth=2/></outer>", ValueModule.display(changed.after!.Value));
    }

    // ---- versioning -------------------------------------------------------------

    [TestMethod]
    public void EveryDocumentCarriesItsVersion()
    {
        var node = JsonNode.Parse(LogFormat.Write(Transaction(Event.NewFileCreated(Record()))))!.AsObject();

        Assert.AreEqual(LogFormat.Version, node["v"]!.GetValue<int>());

        // Pinned against what was written rather than against the constant itself, which
        // the analyser rightly calls always true: a new version has to change this line.
        Assert.AreEqual(2, node["v"]!.GetValue<int>());
    }

    /// <summary>A version this build does not know is refused, by name.</summary>
    /// <remarks>
    /// A log that cannot be read is a user's filesystem that cannot be opened, so it
    /// says which version it found rather than failing somewhere deeper as a null
    /// reference.
    /// </remarks>
    [TestMethod]
    public void AFutureVersionIsRefusedWithItsNumber()
    {
        var node = JsonNode.Parse(LogFormat.Write(Transaction(Event.NewFileCreated(Record()))))!.AsObject();
        node["v"] = 99;

        var error = Assert.ThrowsExactly<FormatException>(() => LogFormat.Read(node.ToJsonString()));

        StringAssert.Contains(error.Message, "version 99");
        StringAssert.Contains(error.Message, "reads up to 2");
    }

    [TestMethod]
    public void ADocumentWithNoVersionIsRefused()
    {
        var error = Assert.ThrowsExactly<FormatException>(() => LogFormat.Read("""{"seq":1}"""));

        StringAssert.Contains(error.Message, "no version");
    }

    [TestMethod]
    public void AnUnknownEventTypeIsRefusedByName()
    {
        var node = JsonNode.Parse(LogFormat.Write(Transaction(Event.NewFileCreated(Record()))))!.AsObject();
        node["events"]!.AsArray()[0]!["type"] = "somethingNew";

        var error = Assert.ThrowsExactly<FormatException>(() => LogFormat.Read(node.ToJsonString()));

        StringAssert.Contains(error.Message, "somethingNew");
    }

    /// <summary>
    /// A log written before decision 0018 has no `undoable` field, and stays readable.
    /// </summary>
    /// <remarks>
    /// Defaulting it to true is what that log meant: every transaction in it was the
    /// user's to take back, because nothing else was possible yet.
    /// </remarks>
    [TestMethod]
    public void ALogWrittenBeforeUndoabilityExistedStillReads()
    {
        var node = JsonNode.Parse(LogFormat.Write(Transaction(Event.NewFileCreated(Record()))))!.AsObject();
        node.Remove("undoable");

        Assert.IsTrue(LogFormat.Read(node.ToJsonString()).Undoable);
    }

    /// <summary>A stored sample from this version still decodes.</summary>
    /// <remarks>
    /// Written out by hand rather than produced by the writer, so it keeps testing the
    /// reader after the writer changes. This is the one case that would catch a shape
    /// change that round-trips with itself but not with what is already on disk.
    /// </remarks>
    [TestMethod]
    public void AStoredVersionOneSampleDecodes()
    {
        const string stored = """
        {
          "v": 1,
          "seq": 2,
          "at": "2026-09-21T12:34:56.7890000+02:00",
          "source": "write notes.txt hello",
          "undoable": true,
          "compensates": null,
          "events": [
            {
              "type": "fileCreated",
              "record": {
                "id": "id-1",
                "attributes": {
                  "folder": { "k": "text", "v": "/" },
                  "kind": { "k": "text", "v": "text" },
                  "name": { "k": "text", "v": "notes.txt" }
                },
                "content": null
              }
            },
            { "type": "contentChanged", "id": "id-1", "before": null, "after": "abc123" }
          ]
        }
        """;

        var read = LogFormat.Read(stored);

        Assert.AreEqual(2L, read.Seq);
        Assert.AreEqual("write notes.txt hello", read.Source);
        Assert.AreEqual(At, read.At);
        Assert.AreEqual(2, read.Events.Length);

        var created = (Event.FileCreated)read.Events[0];
        Assert.AreEqual("notes.txt", Attributes_.Text(created.Item, "name"));

        var content = (Event.ContentChanged)read.Events[1];
        Assert.IsNull(content.before);
        Assert.AreEqual("abc123", content.after!.Value);
    }
}

/// Reads one attribute of a record, which the F# module does by a name C# cannot use.
internal static class Attributes_
{
    public static string Text(FileRecord record, string name) =>
        ValueModule.display(record.Attributes[name]);
}
