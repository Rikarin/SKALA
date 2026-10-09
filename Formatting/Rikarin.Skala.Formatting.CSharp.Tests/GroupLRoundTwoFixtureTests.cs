namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     Group L's round-two shapes, pinned by their oracle fixtures: #559 (SK-DIV-0393), #560 (0394), #561
///     (0395), #562 (0396), #563 (0397) and #564.
/// </summary>
/// <remarks>
///     ⚠ Each fixture's <c>.expected.cs</c> is the oracle's own answer (<c>Testing oracle --only</c>, 2026-10-08),
///     and each was asked shape by shape with <c>Testing ask</c> before it was wired. The fixture is
///     compared whole, so a shape that drifts fails here as well as in the conformance sweep.
/// </remarks>
public sealed class GroupLRoundTwoFixtureTests {
    [Theory]
    [InlineData("breaks/positional-pattern-closer.cs")]
    [InlineData("indentation/pattern-chain-left-of-a-logical-operator.cs")]
    [InlineData("breaks/subpattern-value-under-a-kept-colon-break.cs")]
    [InlineData("wrapping/list-pattern-after-a-broken-is.cs")]
    [InlineData("wrapping/stepped-chain-as-an-argument.cs")]
    [InlineData("breaks/when-list-under-a-kept-arrow-break.cs")]
    // Round three: #559's fill, #575 and #576.
    [InlineData("breaks/positional-pattern-fill.cs")]
    [InlineData("breaks/case-label-braces-under-a-kept-when.cs")]
    [InlineData("breaks/arm-when-condition-below.cs")]
    [InlineData("breaks/arm-body-without-a-break-point.cs")]
    [InlineData("breaks/arm-braces-under-an-arrow-broken-for-width.cs")]
    [InlineData("breaks/arm-when-chain-under-an-arrow-broken-for-width.cs")]
    public void TheFixture_ComesBackAsTheOracleWritesIt(string fixture) {
        var root = Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Testing", "corpus", "constructs");
        var source = File.ReadAllText(Path.Combine(root, fixture)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var expected = File.ReadAllText(Path.Combine(root, fixture[..^3] + ".expected.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        // The first line of an oracle fixture is its provenance header.
        expected = expected[(expected.IndexOf('\n', StringComparison.Ordinal) + 1)..];
        Oracle.Agrees(source, expected);
    }
}
