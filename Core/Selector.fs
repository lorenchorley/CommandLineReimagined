/// CSS selectors over a tag's tree (decision 0049).
///
/// `pick` reads a tag as a document whose root is that tag and answers every element a
/// selector matches. Compiled after `Table.fs`, since the answer is a table, and before
/// the commands, which call it. Phase 11's stream B fills this in.
namespace CommandLineReimagined.Core

[<RequireQualifiedAccess>]
module Selector =

    /// The subset of CSS decision 0049 names, as the reference to build against.
    let supported =
        [ "type"; "*"; "[attr]"; "[attr=value]"; "[attr^=value]"; "[attr$=value]"; "[attr*=value]"
          "descendant ( )"; "child (>)"; "groups (,)" ]
