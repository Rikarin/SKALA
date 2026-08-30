using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>align_multiline_comments</c>: a starred block comment's asterisks. SK-DIV-0033.
/// </summary>
/// <remarks>
///     ⚠ <b>The expectations are <c>jb cleanupcode</c> 2025.2.6's own bytes</b>, taken under
///     <c>OracleProfile.FormatOnly</c> with this repository's <c>.editorconfig</c> and the key at each
///     value, one value per invocation.
///     <para>
///         ⚠ <c>true</c> is the export's own value, which is what makes this a divergence at the shipping
///         configuration rather than an unimplemented option — and what makes it <em>not</em>
///         SK-DIV-0001's principle. That entry refuses to rewrite what the compiler cannot see, i.e.
///         inactive <c>#if</c> text. A block comment is live code that the oracle realigns and the export
///         asks it to; the resemblance is superficial, and Skala already rewrites the interior of a
///         <c>///</c> comment by default (SK-DIV-0006), so "we do not touch the inside of a comment" was
///         never a position this formatter held.
///     </para>
/// </remarks>
public sealed class AlignMultilineCommentTests {
    /// <summary>
    ///     One probe carrying every shape the key sorts on, plus the shapes it must leave alone.
    /// </summary>
    const string Probe =
        "/*\n"
        + " * A starred block comment at file scope.\n"
        + "   * A line whose asterisk is out of place.\n"
        + "* Another one, at column zero.\n"
        + " */\n"
        + "\n"
        + "namespace Skala.Probe;\n"
        + "\n"
        + "public class Commented {\n"
        + "        /*\n"
        + "    * A starred block comment indented inside a type, opener written at column 8.\n"
        + "* At column zero.\n"
        + "          * Far to the right.\n"
        + "     */\n"
        + "    public int First;\n"
        + "\n"
        + "    /*\n"
        + "       A block comment with no leading asterisks at all.\n"
        + "   Its lines are ragged on purpose.\n"
        + "     */\n"
        + "    public int Second;\n"
        + "\n"
        + "        /* Single-line, opener written at 8. */\n"
        + "    public int Third;\n"
        + "\n"
        + "    /*\n"
        + "     * Text after the asterisk\n"
        + "     *   including one indented past it.\n"
        + "     *\n"
        + "     * And a bare asterisk line above.\n"
        + "     */\n"
        + "    public int Fourth;\n"
        + "\n"
        + "        /*\n"
        + "         * Starred and already aligned, but the opener is at 8.\n"
        + "         */\n"
        + "    public int AlignedAtEight;\n"
        + "\n"
        + "    public void Method() {\n"
        + "        /*\n"
        + "     * Inside a method body, opener at column 8.\n"
        + "           * Ragged.\n"
        + "         */\n"
        + "        First = 1;\n"
        + "    }\n"
        + "}\n";

    /// <summary>The oracle's output at the export's <c>true</c>, byte for byte.</summary>
    /// <remarks>
    ///     ⚠ The anchor is the opening <c>/*</c>'s column plus one and not the code's indent, which the
    ///     method-body comment is here to show: its opener sits at 8 and its asterisks land on 9, while
    ///     the type-member comment's opener is pulled to 4 and its asterisks to 5. The closing
    ///     <c>*/</c>'s line moves with the rest. ⚠ The unstarred comment is left exactly as written,
    ///     ragged lines and all.
    /// </remarks>
    const string Aligned =
        "/*\n"
        + " * A starred block comment at file scope.\n"
        + " * A line whose asterisk is out of place.\n"
        + " * Another one, at column zero.\n"
        + " */\n"
        + "\n"
        + "namespace Skala.Probe;\n"
        + "\n"
        + "public class Commented {\n"
        + "    /*\n"
        + "     * A starred block comment indented inside a type, opener written at column 8.\n"
        + "     * At column zero.\n"
        + "     * Far to the right.\n"
        + "     */\n"
        + "    public int First;\n"
        + "\n"
        + "    /*\n"
        + "       A block comment with no leading asterisks at all.\n"
        + "   Its lines are ragged on purpose.\n"
        + "     */\n"
        + "    public int Second;\n"
        + "\n"
        + "    /* Single-line, opener written at 8. */\n"
        + "    public int Third;\n"
        + "\n"
        + "    /*\n"
        + "     * Text after the asterisk\n"
        + "     *   including one indented past it.\n"
        + "     *\n"
        + "     * And a bare asterisk line above.\n"
        + "     */\n"
        + "    public int Fourth;\n"
        + "\n"
        + "    /*\n"
        + "     * Starred and already aligned, but the opener is at 8.\n"
        + "     */\n"
        + "    public int AlignedAtEight;\n"
        + "\n"
        + "    public void Method() {\n"
        + "        /*\n"
        + "         * Inside a method body, opener at column 8.\n"
        + "         * Ragged.\n"
        + "         */\n"
        + "        First = 1;\n"
        + "    }\n"
        + "}\n";

    static string Format(string source, string value) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [new KeyValuePair<string, string>("resharper_csharp_align_multiline_comments", value)]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options)
            .Formatted.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    [Fact]
    public void AtTheExportsValue_SkalaReproducesTheOracle() => Assert.Equal(Aligned, Format(Probe, "true"));

    /// <summary>Formatting the aligned output again changes nothing.</summary>
    /// <remarks>
    ///     ⚠ The realignment reads the comment's <em>written</em> columns to decide whether it qualifies
    ///     and the opener's <em>laid-out</em> column to decide where the asterisks go, so a second pass
    ///     asks a different question of a different input. That it converges is a property to assert, not
    ///     one to assume.
    /// </remarks>
    [Fact]
    public void TheAlignmentIsAFixedPoint() => Assert.Equal(Aligned, Format(Aligned, "true"));

    /// <summary>
    ///     ⚠ Every shape that must be left alone, and each for its own reason.
    /// </summary>
    /// <remarks>
    ///     Measured at <c>true</c>: the oracle returns each of these exactly as written, so a rule that
    ///     recognised "a comment with asterisks in it" rather than "a comment whose <em>every</em>
    ///     continuation line begins with one" would move all four. The blank-line case is the one a
    ///     javadoc-shaped heuristic would get wrong.
    /// </remarks>
    [Theory]
    // First continuation starred, second not.
    [InlineData("class C {\n    /*\n   * One.\n      Two.\n     */\n    int F;\n}\n")]
    // First continuation unstarred, second starred.
    [InlineData("class C {\n    /*\n      One.\n   * Two.\n     */\n    int F;\n}\n")]
    // Every line starred, but with an empty line among them.
    [InlineData("class C {\n    /*\n   * One.\n\n        * Two.\n     */\n    int F;\n}\n")]
    // Every line starred, but with a whitespace-only line among them.
    [InlineData("class C {\n    /*\n   * One.\n   \n        * Two.\n     */\n    int F;\n}\n")]
    public void ADisqualifiedComment_IsReturnedExactlyAsWritten(string source) =>
        Assert.Equal(source, Format(source, "true"));

    /// <summary>
    ///     A block comment that begins on a code line anchors on its own <c>/*</c>, wherever that lands.
    /// </summary>
    [Fact]
    public void ATrailingBlockComment_AnchorsOnItsOwnOpener() {
        const string source = "class C {\n    public int Trailing; /*\n   * Body.\n     */\n}\n";
        const string expected = "class C {\n"
            + "    public int Trailing; /*\n"
            + "                          * Body.\n"
            + "                          */\n"
            + "}\n";
        Assert.Equal(expected, Format(source, "true"));
    }

    /// <summary>
    ///     ⚠ At <c>false</c> Skala still moves the opening <c>/*</c>, and the oracle does not. This
    ///     asserts the gap rather than hiding it.
    /// </summary>
    /// <remarks>
    ///     The oracle freezes a starred comment <em>entire</em> at <c>false</c> — measured, on a comment
    ///     whose opener is written at 8 where the code indent is 4, it comes back at 8. Skala re-indents
    ///     the opener at both values, which it did before this key was read at all. That is a second and
    ///     separable behaviour, about where the comment token starts rather than about its asterisks, and
    ///     it is why this key is registered <c>OfUnoracled</c> rather than promoted: it is honoured,
    ///     observable, and not conformant at one of its two values. SK-DIV-0033 carries the probe.
    ///     <para>
    ///         ⚠ Asserted as the *current* behaviour, so that implementing the freeze fails here and has to
    ///         come back and delete this test. A gap nothing asserts is a gap nobody notices closing.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AtFalse_SkalaStillReindentsTheOpener_WhichTheOracleDoesNot() {
        const string source = "class C {\n        /*\n    * Body.\n     */\n    int F;\n}\n";

        // What the oracle returns at `false`: the comment untouched, opener at 8.
        Assert.NotEqual(source, Format(source, "false"));

        // What Skala returns: the opener pulled to 4, the body left as written.
        Assert.Equal("class C {\n    /*\n    * Body.\n     */\n    int F;\n}\n", Format(source, "false"));
    }
}
