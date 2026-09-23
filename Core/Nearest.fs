/// Which known words a mistyped one was probably meant to be (Phase 8).
///
/// Used twice: by completion, so `delet` still finds `rm`, and by the evaluator, so
/// `lss` says `Did you mean ls?` rather than only that it is unknown. Registered before
/// the evaluator for that second use.
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module Nearest =

    /// <summary>The edit distance between two words, ignoring case.</summary>
    /// <remarks>
    /// Stream A makes this Damerau–Levenshtein. Until then it is plain Levenshtein,
    /// which differs only in counting a swapped pair of letters as two edits.
    /// </remarks>
    let distance (a: string) (b: string) : int =
        let a = a.ToLowerInvariant()
        let b = b.ToLowerInvariant()
        let previous = Array.init (b.Length + 1) id
        let current = Array.zeroCreate (b.Length + 1)

        for i in 1 .. a.Length do
            current[0] <- i

            for j in 1 .. b.Length do
                let cost = if a[i - 1] = b[j - 1] then 0 else 1
                current[j] <- min (min (current[j - 1] + 1) (previous[j] + 1)) (previous[j - 1] + cost)

            Array.blit current 0 previous 0 current.Length

        previous[b.Length]

    /// <summary>The candidates a word was probably meant to be, nearest first.</summary>
    /// <remarks>
    /// Empty until stream A decides the threshold and the ranking, so no message changes
    /// before then.
    /// </remarks>
    let names (candidates: string list) (word: string) : string list = []
