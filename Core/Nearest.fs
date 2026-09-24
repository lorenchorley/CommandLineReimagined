/// Which known words a mistyped one was probably meant to be (Phase 8).
///
/// Used twice: by completion, so `lss` still finds `ls`, and by the evaluator, so
/// `lss` says `Did you mean ls?` rather than only that it is unknown. Registered before
/// the evaluator for that second use. From Phase 9 a word can also name a command by
/// one of its keywords, which is how an old name leads to the new one (decision 0037):
/// `cd` says `Did you mean in?`.
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
