/// XML documents, read into the tree the tag notation produces and written back out of it.
///
/// Decision 0011: one tree, two notations. An element is a `Tag` — its name the type,
/// its attributes typed by the same number-or-text rule a bare word gets, its child
/// elements the children — so a document whose root is table-shaped is a table
/// wherever one is expected, for exactly the reason a written tag is (decision 0009).
/// Nothing here knows about files: the commands read the content and hand it over.
namespace CommandLineReimagined.Core

open System
open System.IO
open System.Text
open System.Xml
open System.Xml.Linq

/// What `to-xml` was told about the names it has to invent.
type XmlOptions =
    { /// The root element's name, instead of the tag's own, `table` or `list`.
      Root: string option
      /// A table's row element, instead of `row`.
      Row: string option
      /// Whether to begin with `<?xml version="1.0" encoding="UTF-8"?>`.
      Declaration: bool }

[<RequireQualifiedAccess>]
module Xml =

    /// <summary>The attribute an element's text is read into (decision 0025).</summary>
    let textAttribute = "text"

    /// The root a table is written under when nothing names one.
    let tableRoot = "table"

    /// The root a list of tags is written under when nothing names one.
    let listRoot = "list"

    let defaults = { Root = None; Row = None; Declaration = false }

    // ---------------------------------------------------------------- Reading

    /// <summary>A name as it was written in the document, prefix and all.</summary>
    /// <remarks>
    /// The tree has no namespaces, so a prefixed name is kept as the text it was, and
    /// the declarations that give the prefixes their meaning are kept as the attributes
    /// they were. Writing both back out gives the document its namespaces again.
    /// </remarks>
    let private writtenName (element: XElement) (name: XName) =
        if name.Namespace = XNamespace.None then
            name.LocalName
        else
            match element.GetPrefixOfNamespace name.Namespace with
            | null
            | "" -> name.LocalName
            | prefix -> prefix + ":" + name.LocalName

    let private attributeName (element: XElement) (attribute: XAttribute) =
        if attribute.IsNamespaceDeclaration then
            if attribute.Name.Namespace = XNamespace.None then "xmlns"
            else "xmlns:" + attribute.Name.LocalName
        else
            writtenName element attribute.Name

    /// <summary>The element's own text, as decision 0025 reads it.</summary>
    /// <remarks>
    /// Text that is only whitespace is the document's indentation rather than anything
    /// it says, so it is dropped; the rest is trimmed and joined with one space, which
    /// is lossless for an element with one run of text and documented as lossy for
    /// mixed content. CDATA is text.
    /// </remarks>
    let private ownText (element: XElement) =
        element.Nodes()
        |> Seq.choose (fun node ->
            match node with
            | :? XText as text when not (String.IsNullOrWhiteSpace text.Value) -> Some(text.Value.Trim())
            | _ -> Option.None)
        |> List.ofSeq

    let rec private ofElement (element: XElement) : Tag =
        let written =
            element.Attributes()
            |> Seq.map (fun attribute -> attributeName element attribute, Value.ofWord attribute.Value)
            |> List.ofSeq

        let attributes =
            match ownText element with
            | [] -> written
            | parts ->
                // The content wins over an XML attribute of the same name: the content is
                // what the document is about, and there is only room for one.
                (written |> List.filter (fun (name, _) -> name <> textAttribute))
                @ [ textAttribute, Value.ofWord (String.concat " " parts) ]

        let children = element.Elements() |> Seq.map (ofElement >> Value.Object) |> List.ofSeq

        Tag.create (writtenName element element.Name) attributes children

    /// <summary>Parses a document into the tree the notation produces.</summary>
    /// <remarks>
    /// A DTD is refused rather than processed: a file in the store is anyone's, and an
    /// entity that expands into itself a billion times is a way to stop a tab. Comments
    /// and processing instructions are dropped, because the tree has nowhere to hold
    /// them. The parser's exception stops here, as the one fault a malformed document
    /// is, naming where the parser gave up.
    /// </remarks>
    let read (path: string) (content: string) : Outcome<Tag> =
        let settings =
            XmlReaderSettings(
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            )

        try
            use reader = XmlReader.Create(new StringReader(content), settings)
            let document = XDocument.Load(reader, LoadOptions.SetLineInfo)
            Ok(ofElement document.Root)
        with :? XmlException as error ->
            // An empty document fails before the parser has read a line, and says line
            // zero; the first line is where the missing element should have been.
            Error(Fault.notWellFormedXml path (max 1 error.LineNumber) (max 1 error.LinePosition))

    // ---------------------------------------------------------------- Writing

    /// Names are checked rather than trusted: a table's columns can come from a CSV
    /// header with a space in it, and XML with a space in a name is not XML.
    let private checkName (name: string) : Outcome<string> =
        try
            XmlConvert.VerifyName name |> ignore
            Ok name
        with
        | :? XmlException
        | :? ArgumentException -> Error(Fault.notAnXmlName name)

    /// Line breaks and tabs in text are written as references, so that a reader's
    /// line-end normalisation cannot change what comes back.
    let private escapeText (text: string) =
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", "&#xD;")

    let private escapeAttribute (text: string) =
        (escapeText text).Replace("\"", "&quot;").Replace("\n", "&#xA;").Replace("\t", "&#x9;")

    let private childTag (value: Value) =
        match value with
        | Value.Object tag
        | Value.Component tag -> Ok tag
        | other -> Error(Fault.cannotWriteAsXml (Value.kind other))

    /// <summary>One element and everything under it, indented two spaces a level.</summary>
    /// <remarks>
    /// Decision 0025: an attribute called `text` is the element's content, written
    /// before its children. An element with text and no children is kept on one line,
    /// so a reader that does not trim still reads back exactly what was written. A gap
    /// is an attribute that is not there, the same as it is in a row.
    /// </remarks>
    let rec private writeElement (output: StringBuilder) (depth: int) (tag: Tag) : Outcome<unit> =
        let indent = String(' ', depth * 2)
        let pairs = Value.orderedAttributes tag |> List.filter (fun (_, value) -> not (Value.isAbsent value))
        let text = pairs |> List.tryFind (fun (name, _) -> name = textAttribute) |> Option.map (snd >> Value.dataText)
        let attributes = pairs |> List.filter (fun (name, _) -> name <> textAttribute)

        let rec writeChildren remaining =
            match remaining with
            | [] -> Ok()
            | child :: rest ->
                childTag child
                |> Outcome.bind (writeElement output (depth + 1))
                |> Outcome.bind (fun () -> writeChildren rest)

        outcome {
            let! name = checkName tag.TypeName
            let! _ = attributes |> Outcome.traverse (fst >> checkName)

            let opening =
                attributes
                |> List.map (fun (key, value) -> sprintf " %s=\"%s\"" key (escapeAttribute (Value.dataText value)))
                |> String.concat ""
                |> fun written -> "<" + name + written

            match text, tag.Children with
            | Option.None, [] -> output.Append(indent).Append(opening).Append("/>\n") |> ignore
            | Some text, [] ->
                output.Append(indent).Append(opening).Append(">").Append(escapeText text).Append("</" + name + ">\n")
                |> ignore
            | text, children ->
                output.Append(indent).Append(opening).Append(">\n") |> ignore

                match text with
                | Some text -> output.Append(indent).Append("  ").Append(escapeText text).Append("\n") |> ignore
                | Option.None -> ()

                do! writeChildren children
                output.Append(indent).Append("</" + name + ">\n") |> ignore
        }

    /// <summary>The element a value is written as.</summary>
    /// <remarks>
    /// A tag is itself. A table is a root of rows, which is `Table.toTag` with the two
    /// names it would otherwise invent open to the caller. A list is a root around its
    /// items, which must each be a tag and keep their own names.
    /// </remarks>
    let private rootOf (options: XmlOptions) (value: Value) : Outcome<Tag> =
        match value with
        | Value.Object tag
        | Value.Component tag ->
            Ok { tag with TypeName = defaultArg options.Root tag.TypeName }
        | Value.Table table ->
            let row = defaultArg options.Row Table.rowType

            Ok(
                Tag.create
                    (defaultArg options.Root tableRoot)
                    []
                    (table.Rows |> List.map (Table.rowTag row table >> Value.Object))
            )
        | Value.List items -> Ok(Tag.create (defaultArg options.Root listRoot) [] items)
        | other -> Error(Fault.cannotWriteAsXml (Value.kind other))

    /// <summary>A value as a document: pretty-printed, UTF-8, ending in a line break.</summary>
    /// <remarks>
    /// Written by hand rather than through `XmlWriter`, which will not write a prefixed
    /// name without being told the namespace behind it, and the tree only knows the
    /// prefix. The names are checked here instead.
    /// </remarks>
    let write (options: XmlOptions) (value: Value) : Outcome<string> =
        outcome {
            let! root = rootOf options value
            let output = StringBuilder()

            if options.Declaration then
                output.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n") |> ignore

            do! writeElement output 0 root
            return output.ToString()
        }
