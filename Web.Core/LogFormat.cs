using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommandLineReimagined.Core;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace CommandLineReimagined.Web.Persistence;

/// <summary>
/// The on-disk shape of a transaction.
/// </summary>
/// <remarks>
/// Hand-written, and deliberately not a serialiser pointed at the F# types. What is
/// written here is read back by a later build, so the shape is a contract: renaming a
/// union case or reordering a record's fields must not be able to make somebody's
/// filesystem unreadable. Every document carries <c>"v"</c>, and a reader exists per
/// version, so a change to the shape is a new writer and one more reader rather than a
/// migration nobody can test.
///
/// Values are written by kind rather than as JSON's own types, because a file
/// attribute can be text that happens to look like a number and it has to come back as
/// text.
/// </remarks>
public static class LogFormat
{
    /// <summary>The version this build writes. Readers exist for this and every earlier one.</summary>
    public const int Version = 1;

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    // ------------------------------------------------------------------- writing

    public static string Write(Transaction transaction) => ToNode(transaction).ToJsonString(Compact);

    public static JsonObject ToNode(Transaction transaction) =>
        new()
        {
            ["v"] = Version,
            ["seq"] = transaction.Seq,
            // Round-trip format, so a timestamp read back is the instant that was
            // written rather than one rounded to the nearest second.
            ["at"] = transaction.At.ToString("o", CultureInfo.InvariantCulture),
            ["source"] = transaction.Source,
            ["undoable"] = transaction.Undoable,
            ["compensates"] = transaction.Compensates is { } c ? JsonValue.Create(c.Value) : null,
            ["events"] = new JsonArray(transaction.Events.Select(ToNode).ToArray<JsonNode?>()),
        };

    private static JsonObject ToNode(Event e) =>
        e switch
        {
            Event.FileCreated created => new JsonObject
            {
                ["type"] = "fileCreated",
                ["record"] = ToNode(created.Item),
            },

            Event.FileDeleted deleted => new JsonObject
            {
                ["type"] = "fileDeleted",
                ["record"] = ToNode(deleted.Item),
            },

            Event.AttributesChanged attributes => new JsonObject
            {
                ["type"] = "attributesChanged",
                ["id"] = attributes.id,
                ["before"] = ToNode(attributes.before),
                ["after"] = ToNode(attributes.after),
            },

            Event.ContentChanged content => new JsonObject
            {
                ["type"] = "contentChanged",
                ["id"] = content.id,
                ["before"] = content.before is { } b ? JsonValue.Create(b.Value) : null,
                ["after"] = content.after is { } a ? JsonValue.Create(a.Value) : null,
            },

            Event.VariableChanged variable => new JsonObject
            {
                ["type"] = "variableChanged",
                ["name"] = variable.name,
                ["before"] = variable.before is { } vb ? ToNode(vb.Value) : null,
                ["after"] = variable.after is { } va ? ToNode(va.Value) : null,
            },

            Event.LocationChanged location => new JsonObject
            {
                ["type"] = "locationChanged",
                ["before"] = ToNode(location.before),
                ["after"] = ToNode(location.after),
            },

            _ => throw new NotSupportedException(
                $"No stored shape for the event {e.GetType().Name}. Adding an event case means " +
                "adding it here and bumping the version."),
        };

    private static JsonObject ToNode(FileRecord record) =>
        new()
        {
            ["id"] = record.Id,
            ["attributes"] = ToNode(record.Attributes),
            ["content"] = record.Content is { } hash ? JsonValue.Create(hash.Value) : null,
        };

    private static JsonObject ToNode(Location location) =>
        new()
        {
            ["folder"] = location.Folder,
            // A saved query is Phase 4; the field is written now so that a log from
            // this build is readable by that one without a version bump.
            ["view"] = null,
        };

    private static JsonObject ToNode(FSharpMap<string, Value> attributes)
    {
        var node = new JsonObject();

        foreach (var pair in attributes)
        {
            node[pair.Key] = ToNode(pair.Value);
        }

        return node;
    }

    /// <summary>
    /// A value, tagged with its kind.
    /// </summary>
    /// <remarks>
    /// Not written as the nearest JSON type. A `tag` attribute whose text is `2026`
    /// has to come back as text, and JSON cannot tell that from a number without being
    /// told.
    /// </remarks>
    private static JsonObject ToNode(Value value)
    {
        if (value.IsEmpty) return new JsonObject { ["k"] = "empty" };
        if (value.IsNone) return new JsonObject { ["k"] = "none" };

        return value switch
        {
            Value.Text text => new JsonObject { ["k"] = "text", ["v"] = text.Item },
            Value.Number number => new JsonObject { ["k"] = "number", ["v"] = number.Item },
            Value.Boolean boolean => new JsonObject { ["k"] = "boolean", ["v"] = boolean.Item },

            Value.File file => new JsonObject
            {
                ["k"] = "file",
                ["id"] = file.Item.Id,
                ["name"] = file.Item.Name,
                ["kind"] = file.Item.Kind,
                ["folder"] = file.Item.Folder,
            },

            Value.List list => new JsonObject
            {
                ["k"] = "list",
                ["items"] = new JsonArray(list.Item.Select(ToNode).ToArray<JsonNode?>()),
            },

            Value.Object tag => TagNode("object", tag.Item),
            Value.Component tag => TagNode("component", tag.Item),

            _ => throw new NotSupportedException(
                $"No stored shape for the value {value.GetType().Name}. Adding a value case means " +
                "adding it here and bumping the version."),
        };
    }

    private static JsonObject TagNode(string kind, Tag tag) =>
        new()
        {
            ["k"] = kind,
            ["type"] = tag.TypeName,
            ["attributes"] = ToNode(tag.Attributes),
            ["children"] = new JsonArray(tag.Children.Select(ToNode).ToArray<JsonNode?>()),
        };

    // ------------------------------------------------------------------- reading

    /// <summary>
    /// Reads a stored transaction, or throws with what was wrong.
    /// </summary>
    /// <remarks>
    /// A log that cannot be read is a user's filesystem that cannot be opened, so the
    /// message names the version rather than failing as a null reference somewhere
    /// deeper. The caller decides what to do about it; `reset` is the way out.
    /// </remarks>
    public static Transaction Read(string json)
    {
        var node = JsonNode.Parse(json)?.AsObject()
            ?? throw new FormatException("A stored transaction was not a JSON object.");

        int version = node["v"]?.GetValue<int>()
            ?? throw new FormatException("A stored transaction carries no version.");

        return version switch
        {
            1 => ReadVersion1(node),
            _ => throw new FormatException(
                $"A stored transaction is version {version}; this build reads up to {Version}."),
        };
    }

    private static Transaction ReadVersion1(JsonObject node) =>
        new(
            node["seq"]!.GetValue<long>(),
            DateTimeOffset.Parse(node["at"]!.GetValue<string>(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            node["source"]!.GetValue<string>(),
            ListOf(node["events"]!.AsArray().Select(e => ReadEvent(e!.AsObject()))),
            node["compensates"] is { } compensates
                ? FSharpOption<long>.Some(compensates.GetValue<long>())
                : FSharpOption<long>.None,
            // Absent in a log written before decision 0018, where every transaction
            // was the user's to take back. Defaulting to true keeps those readable and
            // behaving as they did.
            node["undoable"]?.GetValue<bool>() ?? true);

    private static Event ReadEvent(JsonObject node) =>
        node["type"]!.GetValue<string>() switch
        {
            "fileCreated" => Event.NewFileCreated(ReadRecord(node["record"]!.AsObject())),
            "fileDeleted" => Event.NewFileDeleted(ReadRecord(node["record"]!.AsObject())),

            "attributesChanged" => Event.NewAttributesChanged(
                node["id"]!.GetValue<string>(),
                ReadAttributes(node["before"]!.AsObject()),
                ReadAttributes(node["after"]!.AsObject())),

            "contentChanged" => Event.NewContentChanged(
                node["id"]!.GetValue<string>(),
                ReadOptionalString(node["before"]),
                ReadOptionalString(node["after"])),

            "variableChanged" => Event.NewVariableChanged(
                node["name"]!.GetValue<string>(),
                ReadOptionalValue(node["before"]),
                ReadOptionalValue(node["after"])),

            "locationChanged" => Event.NewLocationChanged(
                ReadLocation(node["before"]!.AsObject()),
                ReadLocation(node["after"]!.AsObject())),

            var unknown => throw new FormatException(
                $"A stored transaction holds an event of type '{unknown}', which this build does not know."),
        };

    private static FileRecord ReadRecord(JsonObject node) =>
        new(node["id"]!.GetValue<string>(),
            ReadAttributes(node["attributes"]!.AsObject()),
            ReadOptionalString(node["content"]));

    private static Location ReadLocation(JsonObject node) =>
        // The view stays None until Phase 4 gives it a shape to read.
        new(node["folder"]!.GetValue<string>(), FSharpOption<Expr>.None);

    private static FSharpMap<string, Value> ReadAttributes(JsonObject node) =>
        MapModule.OfSeq(node.Select(pair =>
            Tuple.Create(pair.Key, ReadValue(pair.Value!.AsObject()))));

    private static Value ReadValue(JsonObject node) =>
        node["k"]!.GetValue<string>() switch
        {
            "empty" => Value.Empty,
            "none" => Value.None,
            "text" => Value.NewText(node["v"]!.GetValue<string>()),
            "number" => Value.NewNumber(node["v"]!.GetValue<double>()),
            "boolean" => Value.NewBoolean(node["v"]!.GetValue<bool>()),

            "file" => Value.NewFile(new FileRef(
                node["id"]!.GetValue<string>(),
                node["name"]!.GetValue<string>(),
                node["kind"]!.GetValue<string>(),
                node["folder"]!.GetValue<string>())),

            "list" => Value.NewList(ListOf(node["items"]!.AsArray().Select(i => ReadValue(i!.AsObject())))),

            "object" => Value.NewObject(ReadTag(node)),
            "component" => Value.NewComponent(ReadTag(node)),

            var unknown => throw new FormatException(
                $"A stored value is of kind '{unknown}', which this build does not know."),
        };

    private static Tag ReadTag(JsonObject node) =>
        new(node["type"]!.GetValue<string>(),
            ReadAttributes(node["attributes"]!.AsObject()),
            ListOf(node["children"]!.AsArray().Select(c => ReadValue(c!.AsObject()))));

    private static FSharpOption<string> ReadOptionalString(JsonNode? node) =>
        node is null ? FSharpOption<string>.None : FSharpOption<string>.Some(node.GetValue<string>());

    private static FSharpOption<Value> ReadOptionalValue(JsonNode? node) =>
        node is null ? FSharpOption<Value>.None : FSharpOption<Value>.Some(ReadValue(node.AsObject()));

    private static FSharpList<T> ListOf<T>(IEnumerable<T> items) => ListModule.OfSeq(items);
}
