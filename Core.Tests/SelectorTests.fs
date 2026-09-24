namespace CommandLineReimagined.Core.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting
open CommandLineReimagined.Core

/// <summary>CSS selectors and `pick` (decision 0049).</summary>
/// <remarks>Registered by Phase 11's foundation; stream B's tests go here.</remarks>
[<TestClass>]
type SelectorTests() =

    /// The subset the decision names is the one the module says it supports.
    [<TestMethod>]
    member _.TheSubsetIsNamed() =
        Assert.AreEqual<int>(10, List.length Selector.supported)
