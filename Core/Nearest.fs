/// Which known words a mistyped one was probably meant to be (Phase 8).
///
/// Used twice: by completion, so `lss` still finds `ls`, and by the evaluator, so
/// `lss` says `Did you mean ls?` rather than only that it is unknown. Registered before
/// the evaluator for that second use. From Phase 9 a word can also name a command by
/// one of its keywords, which is how an old name leads to the new one (decision 0037):
/// `cd` says `Did you mean in?`. From Phase 10 a missing file, folder or variable names
/// the nearest ones too (decision 0042), in a note with a fix for each (0041, 0044).
namespace CommandLineReimagined.Core

open System

[<RequireQualifiedAccess>]
module Nearest =

    /// <summary>The edit distance between two words, ignoring case.</summary>
    /// <remarks>
    /// Damerau–Levenshtein in its optimal string alignment form: an insertion, a
    /// deletion, a substitution and a swap of two neighbouring letters each count one.
    /// The swap is the point of it: `sotr` is one slip from `sort`, not two.
    /// </remarks>
    let distance (a: string) (b: string) : int =
        let a = a.ToLowerInvariant()
        let b = b.ToLowerInvariant()
        let d = Array2D.zeroCreate (a.Length + 1) (b.Length + 1)

        for i in 0 .. a.Length do
            d[i, 0] <- i

        for j in 0 .. b.Length do
            d[0, j] <- j

        for i in 1 .. a.Length do
            for j in 1 .. b.Length do
                let cost = if a[i - 1] = b[j - 1] then 0 else 1
                let best = min (min (d[i - 1, j] + 1) (d[i, j - 1] + 1)) (d[i - 1, j - 1] + cost)

                d[i, j] <-
                    if i > 1 && j > 1 && a[i - 1] = b[j - 2] && a[i - 2] = b[j - 1] then
                        min best (d[i - 2, j - 2] + 1)
                    else
                        best

        d[a.Length, b.Length]

    /// <summary>How far a word may be from a candidate and still be offered.</summary>
    /// <remarks>
    /// One slip in a word of up to four letters, two in a longer one. Two slips in `ls`
    /// reach `in`, `cp` and `rm`, which is guessing rather than correcting.
    /// </remarks>
    let threshold (word: string) = if word.Length <= 4 then 1 else 2

    /// <summary>The candidates a word was probably meant to be, nearest first.</summary>
    /// <remarks>
    /// Within `threshold`, and not the word itself: a word that is already a candidate
    /// was not mistyped. Equally near candidates keep the order they were given in.
    /// </remarks>
    let names (candidates: string list) (word: string) : string list =
        if String.IsNullOrEmpty word then
            []
        else
            let limit = threshold word

            candidates
            |> List.distinct
            |> List.map (fun candidate -> candidate, distance word candidate)
            |> List.filter (fun (_, d) -> d > 0 && d <= limit)
            |> List.sortBy snd
            |> List.map fst

    let private equal (a: string) (b: string) =
        String.Equals(a, b, StringComparison.OrdinalIgnoreCase)

    /// <summary>The keyword of a command that a word, written whole, is.</summary>
    /// <remarks>
    /// A word of three letters or more is any keyword it equals: `delete` is one of
    /// `rm`'s. A shorter one is a keyword only when that keyword is a name, not a word
    /// the command is described by, and what tells the two apart is where 0037 put the
    /// old names: each is its command's first keyword, and no other command's. `cd` is
    /// `in`'s first keyword and nobody else's, so it names `in`. `by` is `group`'s first
    /// and one of `sort`'s as well, so it is a word two commands are described by and
    /// names neither; `as` is not `table`'s first, so it does not name `table`.
    /// `specs` is every command, which is what "nobody else's" is counted over.
    /// </remarks>
    let keywordOf (specs: CommandSpec list) (spec: CommandSpec) (word: string) : string option =
        if String.IsNullOrEmpty word then
            None
        elif word.Length >= 3 then
            spec.Keywords |> List.tryFind (equal word)
        else
            match spec.Keywords with
            | first :: _ when equal word first ->
                let others =
                    specs
                    |> List.filter (fun other -> not (equal other.Name spec.Name))
                    |> List.exists (fun other -> other.Keywords |> List.exists (equal word))

                if others then None else Some first
            | _ -> None

    /// <summary>The commands a word that names none was probably meant to be.</summary>
    /// <remarks>
    /// A word that is one of a command's keywords (`keywordOf`) was not mistyped: it
    /// says what the command does, or what it used to be called, so the commands it is
    /// a keyword of are the answer, in the order given, and slips are not looked for.
    /// `cd` is `in`'s old name and one slip from `cp`, and it means `in`. Any other word
    /// is corrected as a slip, as `names` corrects it.
    /// </remarks>
    let commands (specs: CommandSpec list) (word: string) : string list =
        let byKeyword =
            specs
            |> List.filter (fun spec -> (keywordOf specs spec word).IsSome)
            |> List.map (fun spec -> spec.Name)
            |> List.distinct

        if List.isEmpty byKeyword then
            names (specs |> List.map (fun spec -> spec.Name)) word
        else
            byKeyword

    // --------------------------------------- Files, folders and variables (0042)

    /// At most this many are named, as for a command.
    let private most = 3

    /// Within `threshold` of the word, and not the word itself; a name that differs
    /// only in case is near, because a path and a variable are looked up exactly.
    let private close (word: string) (candidate: string) =
        candidate <> word && distance word candidate <= threshold word

    /// A name the word is the start of, or the word in another case: `notes` for
    /// `notes.txt`, `fil` for `files`.
    let private startedBy (word: string) (candidate: string) =
        candidate <> word && candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase)

    /// <summary>The variables an unknown one was probably meant to be, nearest first.</summary>
    /// <remarks>
    /// Those within `threshold`, nearest first, and then those it is the start of, each
    /// in the order given; at most three. `row` is never a candidate: nobody sets it.
    /// </remarks>
    let variables (candidates: string list) (word: string) : string list =
        if String.IsNullOrEmpty word then
            []
        else
            let candidates = candidates |> List.distinct |> List.filter (fun name -> name <> "row")
            let near = candidates |> List.filter (close word) |> List.sortBy (distance word)
            let started = candidates |> List.filter (startedBy word)

            near @ started |> List.distinct |> List.truncate most

    /// <summary>The records a missing path was probably meant to be, as absolute paths.</summary>
    /// <remarks>
    /// First the names within `threshold` in the folder it was looked for in, which for
    /// a name written on its own is the current folder, nearest first; then, anywhere,
    /// the same name, and then a name it is the start of, the shallower first. At most
    /// three. A folder is looked for among folders only; anything else among every
    /// record, since `rm` and `cp` take either.
    /// </remarks>
    let paths (projection: Projection) (missing: string) (foldersOnly: bool) : string list =
        let folder, name = Files.split missing

        if String.IsNullOrEmpty name || missing = Files.root then
            []
        else
            let records =
                projection.Files
                |> Map.toList
                |> List.map snd
                |> List.filter (fun record -> not foldersOnly || Record.isFolder record)

            let pathOf record = Value.joinPath (Record.folder record) (Record.name record)
            let depth (path: string) = path |> Seq.filter (fun c -> c = '/') |> Seq.length

            let here =
                records
                |> List.filter (fun record -> Record.folder record = folder && close name (Record.name record))
                |> List.sortBy (fun record -> distance name (Record.name record), Record.name record)
                |> List.map pathOf

            let anywhere =
                records
                |> List.filter (fun record ->
                    let candidate = Record.name record
                    String.Equals(candidate, name, StringComparison.OrdinalIgnoreCase) || startedBy name candidate)
                |> List.map (fun record ->
                    let same = String.Equals(Record.name record, name, StringComparison.OrdinalIgnoreCase)
                    let path = pathOf record
                    (if same then 0 else 1), depth path, path)
                |> List.sort
                |> List.map (fun (_, _, path) -> path)
                |> List.filter (fun path -> path <> missing)

            here @ anywhere |> List.distinct |> List.truncate most

    /// A path as a person would write it standing in `folder`: relative where it is
    /// inside it, absolute otherwise, and quoted when it would not read back as one word.
    let written (folder: string) (path: string) =
        let relative =
            if folder = Files.root then path.Substring 1
            elif path.StartsWith(folder + "/") then path.Substring(folder.Length + 1)
            else path

        Expr.asWritten relative

    /// <summary>The words of a line that are arguments, with where each starts.</summary>
    /// <remarks>
    /// Each is its start, the text as written and the text it stands for, which for a
    /// quoted word is what is inside the quotes. A line is split at spaces, `|`, `(`,
    /// `)` and `=`, so `folder=/nowhere` gives `/nowhere`. The first word of a line or a
    /// stage, and the one after `try`, `else` or `??`, names a command and is left out,
    /// so `read read` corrects the second word. Not a parser: a word it splits wrongly
    /// is only a word that gets no fix.
    /// </remarks>
    let private arguments (source: string) : (int * string * string) list =
        let found = ResizeArray()
        let mutable at = 0
        let mutable command = true

        while at < source.Length do
            let c = source[at]

            if Char.IsWhiteSpace c || c = '=' || c = ')' then
                at <- at + 1
            elif c = '|' || c = '(' then
                command <- true
                at <- at + 1
            else
                let start = at

                if c = '"' then
                    let closing = source.IndexOf('"', at + 1)
                    at <- if closing < 0 then source.Length else closing + 1
                else
                    while at < source.Length
                          && not (Char.IsWhiteSpace source[at])
                          && not ("|()=\"".Contains source[at]) do
                        at <- at + 1

                let word = source.Substring(start, at - start)

                let text =
                    if word.Length >= 2 && word.StartsWith "\"" && word.EndsWith "\"" then
                        word.Substring(1, word.Length - 2)
                    else
                        word

                if word = "try" || word = "else" || word = "??" then
                    command <- true
                elif command then
                    command <- false
                else
                    found.Add((start, word, text))

        List.ofSeq found

    /// <summary>The note for a file or folder that is not there (decisions 0042, 0044).</summary>
    /// <remarks>
    /// `missing` is the fault's path, made absolute against `location` if it is not.
    /// The nearest paths are named as written from the current folder. The fix for each
    /// writes it in place of the argument of `source` that named the missing path, or a
    /// path under it (`mkdir documnts/x` gives `mkdir documents/x`). A line with no such
    /// argument, such as `run` of a script that has the mistake, gets the note and no
    /// fix. Nothing near, no note.
    /// </remarks>
    let pathNote
        (projection: Projection)
        (location: Location)
        (source: string)
        (missing: string)
        (foldersOnly: bool)
        : Note list =
        let missing = Files.normalise location.Folder missing
        let nearest = paths projection missing foldersOnly

        let place =
            arguments source
            |> List.tryPick (fun (start, word, text) ->
                if text.StartsWith "$" || text.StartsWith "-" || text.StartsWith "<" then
                    Option.None
                else
                    let resolved = Files.normalise location.Folder text

                    if Files.isAncestorOf missing resolved then
                        Some(start, word, resolved.Substring missing.Length)
                    else
                        Option.None)

        let fixes =
            match place with
            | Some(start, word, under) ->
                [ for path in nearest ->
                      Fix.Line(
                          source.Substring(0, start)
                          + written location.Folder (path + under)
                          + source.Substring(start + word.Length)
                      ) ]
            | Option.None -> []

        Fault.nearestNote (nearest |> List.map (written location.Folder)) fixes

    /// <summary>The note for a variable that is not set (decisions 0042, 0044).</summary>
    /// <remarks>
    /// `candidates` are the variables there are. The fix writes the nearest in place of
    /// the first `$name` in `source` that is the whole variable, with or without a
    /// member read off it (`$fles.name`); a line without one gets no fix.
    /// </remarks>
    let variableNote (candidates: string list) (source: string) (name: string) : Note list =
        let nearest = variables candidates name
        let said = "$" + name

        let continues (c: char) =
            Char.IsLetterOrDigit c || c = '_' || c = '-'

        let rec find (from: int) =
            match source.IndexOf(said, from, StringComparison.Ordinal) with
            | -1 -> Option.None
            | at ->
                let after = at + said.Length
                let before = at = 0 || not (continues source[at - 1] || source[at - 1] = '.' || source[at - 1] = '$')
                let ends = after = source.Length || not (continues source[after])
                if before && ends then Some at else find (at + 1)

        let fixes =
            match find 0 with
            | Some at ->
                [ for near in nearest -> Fix.Line(source.Substring(0, at) + "$" + near + source.Substring(at + said.Length)) ]
            | Option.None -> []

        Fault.nearestNote (nearest |> List.map (fun near -> "$" + near)) fixes
