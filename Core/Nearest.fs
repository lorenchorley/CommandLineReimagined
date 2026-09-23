/// Which known words a mistyped one was probably meant to be (Phase 8).
///
/// Used twice: by completion, so `lss` still finds `ls`, and by the evaluator, so
/// `lss` says `Did you mean ls?` rather than only that it is unknown. Registered before
/// the evaluator for that second use.
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
    /// reach `cd`, `cp` and `rm`, which is guessing rather than correcting.
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
