using CommandLineReimagined.Web;

namespace Web.Core.Tests;

/// <summary>
/// What the page is told about a tapped token: <see cref="TerminalSession.DescribeAsync"/>.
/// </summary>
/// <remarks>
/// What each token means is settled in `Core.Tests` (`HoverTests`). These check that it
/// arrives as the record the page reads: the F# options become nulls, and the signature
/// the same <see cref="SignatureInfo"/> completion sends.
/// </remarks>
[TestClass]
public class DescribeTests
{
    private TerminalSession _session = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _session = new TerminalSession();
        await _session.InitializeAsync();
    }

    /// The offset the page sends is the end of the token, marked here with `‸`.
    private Task<HoverInfo?> Describe(string line) =>
        _session.DescribeAsync(line.Replace("‸", ""), line.IndexOf('‸'));

    [TestMethod]
    public async Task AVariableArrivesWithItsSummary()
    {
        await _session.ExecuteAsync("set v 5");

        var hover = await Describe("echo $v‸");

        Assert.IsNotNull(hover);
        Assert.AreEqual("variable", hover.Kind);
        Assert.AreEqual("$v", hover.Text);
        StringAssert.StartsWith(hover.Detail, "number");
        Assert.IsNull(hover.Signature);
    }

    [TestMethod]
    public async Task ACommandArrivesWithItsSignature()
    {
        var hover = await Describe("ls | sort‸ name");

        Assert.IsNotNull(hover);
        Assert.AreEqual("command", hover.Kind);
        Assert.IsNotNull(hover.Signature);
        Assert.AreEqual("sort", hover.Signature.Command);
        Assert.AreEqual(hover.Signature.Description, hover.Detail);
        // `sort <column> [desc] [table]`: the table is the one that comes through the pipe.
        CollectionAssert.AreEqual(
            new[] { "column", "desc", "table" },
            hover.Signature.Parameters.Select(p => p.Name).ToArray());
        Assert.IsFalse(hover.Signature.Parameters[0].Optional);
        Assert.IsTrue(hover.Signature.Parameters[1].Optional);
        Assert.IsNull(hover.Signature.Active);
    }

    [TestMethod]
    public async Task AnArgumentMarksItsParameterActive()
    {
        var hover = await Describe("ls | sort name‸");

        Assert.IsNotNull(hover);
        Assert.AreEqual(0, hover.Signature!.Active);
    }

    [TestMethod]
    public async Task ARowMemberArrivesWithItsColumnType()
    {
        var hover = await Describe("ls | where $row.size‸ gt 10");

        Assert.IsNotNull(hover);
        Assert.AreEqual("member", hover.Kind);
        Assert.AreEqual("column · number", hover.Detail);
    }

    [TestMethod]
    public async Task AnOperatorSaysWhatItCompares()
    {
        var hover = await Describe("ls | where $row.kind eq‸ folder");

        Assert.IsNotNull(hover);
        Assert.AreEqual("operator", hover.Kind);
        Assert.AreEqual("true when $row.kind is equal to folder", hover.Detail);
    }

    /// Nothing to say is null, not an empty record, so the page falls back to the
    /// token's grammar role.
    [TestMethod]
    public async Task NothingToSayIsNull()
    {
        Assert.IsNull(await Describe("lss‸"));
    }

    /// The page may send an offset past the end if the line changed under it.
    [TestMethod]
    public async Task AnOffsetOutsideTheTextIsClamped()
    {
        Assert.IsNotNull(await _session.DescribeAsync("ls | sort name", 400));
        await _session.DescribeAsync("ls | sort name", -3);
        Assert.IsNull(await _session.DescribeAsync(null!, 2));
    }
}
