using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     #588: <see cref="Corpus.OracleSymbols" /> is a typed list, and SK-DIV-0004 is the reason a typed list is not
///     trusted. This builds the oracle's scratch project with a binary log and asserts the list is what the SDK
///     really defines for it.
/// </summary>
public sealed class OracleSymbolsTests {
    /// <summary>
    ///     ⚠ A failed probe build fails here rather than skipping: the fallback is <c>DEBUG;TRACE</c>, which would
    ///     read as a mismatch, and a skip would be the silent zero this list exists to avoid.
    /// </summary>
    [Fact]
    public void TheCommittedList_IsWhatTheOraclesProjectDefines() {
        var log = new StringWriter();
        var probed = PreprocessorFidelity.OracleSymbols(log).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            probed.SequenceEqual(Corpus.OracleSymbols.Order(StringComparer.Ordinal)),
            "The oracle's project defines " + string.Join(' ', probed) + ", and Corpus.OracleSymbols says "
            + string.Join(' ', Corpus.OracleSymbols) + ".\n" + log
        );
    }
}
