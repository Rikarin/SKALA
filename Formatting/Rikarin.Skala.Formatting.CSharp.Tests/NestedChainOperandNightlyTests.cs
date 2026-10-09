using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     A nested chain that spans lines is an operand that spans lines, and chops every link of the chain around
///     it (Nightly <c>fuzz --seed=4242</c>, case 5604488888367663423).
/// </summary>
/// <remarks>
///     ⚠ A chain's owner reads its own links' breaks as weak (SK-DIV-0109), and the weakness leaked outward: a
///     broken <c>&amp;&amp;</c> inside the last operand of <c>a + (b * c) + (…)</c> chopped only the link holding
///     it, leaving <c>a + (b * c)</c> on one line; pass two read the <c>+</c> break as the author's and chopped
///     the first link too — the oracle's answer for both inputs. Expected outputs are the oracle's, measured
///     2026-10-09 with <c>Testing ask</c>, except the ternary's indent under a broken condition (the oracle
///     indents <c>?</c> one level deeper), a stable divergence these inputs avoid.
/// </remarks>
public sealed class NestedChainOperandNightlyTests {
    static string FormatWith(string source) {
        var options = new PhaseOneOptions(
            OptionResolver.Resolve(Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"), []).Options
        );

        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    [Theory]
    [InlineData(
        "        var v2 = alpha + (beta * step) + (gamma\n&& delta);\n",
        "        var v2 = alpha\n            + (beta * step)\n            + (gamma\n                && delta);\n"
    )]
    [InlineData(
        "        var r6 = alpha + beta - (gamma\n            * delta) - epsilon;\n",
        "        var r6 = alpha\n            + beta\n            - (gamma\n"
        + "                * delta)\n            - epsilon;\n"
    )]
    [InlineData(
        "        var r7 = first ?? second ?? (third\n            ?? fourth);\n",
        "        var r7 = first\n            ?? second\n            ?? (third\n                ?? fourth);\n"
    )]
    public void ABrokenNestedChain_ChopsTheChainAroundIt(string statement, string expected) {
        var formatted = FormatWith("class C {\n    void M() {\n" + statement + "    }\n}\n");
        Assert.Equal("class C {\n    void M() {\n" + expected + "    }\n}\n", formatted);
        Assert.Equal(formatted, FormatWith(formatted));
    }

    /// <summary>
    ///     ⚠ The chain's own link broken by the author stays weak to its owner: the <c>&amp;&amp;</c> inside the
    ///     first <c>||</c> operand is not chopped. Pinned so the fix cannot be "make every link strong".
    /// </summary>
    [Fact]
    public void AnOuterBreak_LeavesTheInnerChainWhole() {
        const string source =
            "class C {\n    bool M(int a) {\n        return a > 0 && a < 10\n            || a == 20;\n    }\n}\n";
        Assert.Equal(source, FormatWith(source));
    }
}
