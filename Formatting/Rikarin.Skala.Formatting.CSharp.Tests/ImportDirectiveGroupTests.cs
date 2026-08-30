using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>dotnet_separate_import_directive_groups</c>, in the component that owns it. SK-DIV-0074.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         These assertions are <c>jb cleanupcode</c> 2025.2.6's own output under
///         <c>OracleProfile.FormatOnly</c>
///     </b> — the format-only profile, which carries
///     <c>CSReformatCode</c> and no arrangement task whatsoever. That is the finding: the oracle
///     performs <em>both</em> of this key's directions with nothing but the reformat, so it is a
///     formatting key, and while only Skala's arranger read it <c>skala format</c> and
///     <c>skala arrange</c> gave different blank lines for the same file and the same key.
///     <para>
///         ⚠ The order is deliberately left alone by the probe below — no directive here is out of the
///         order the oracle would sort it into under the cleanup profile, so what is measured is the
///         separation and not the sort. Sorting is still <c>UsingsRule</c>'s; the formatter separates
///         whatever order it is handed.
///     </para>
/// </remarks>
public sealed class ImportDirectiveGroupTests {
    /// <summary>
    ///     One probe carrying every distinction the key turns on, and blank runs of 0, 1 and 2.
    /// </summary>
    const string Probe =
        "using System.Text;\n"
        + "\n"
        + "using System.Globalization;\n"
        + "\n"
        + "\n"
        + "using Zeta.Support;\n"
        + "using Zeta.Support.Deep;\n"
        + "using static System.Math;\n"
        + "using static Zeta.Support.Helper;\n"
        + "using AliasOne = System.Collections.Generic.List<int>;\n"
        + "using AliasTwo = Zeta.Support.Helper;\n"
        + "using System.Threading;\n"
        + "\n"
        + "// a comment with a blank line above it\n"
        + "using Alpha.Widgets;\n"
        + "\n"
        + "namespace Skala.Probe;\n";

    /// <summary>The export's own value: every blank line between two adjacent directives goes.</summary>
    /// <remarks>
    ///     ⚠ The blank line above the <em>comment</em> survives, and that corrects the entry's own
    ///     phrasing — "it takes every blank line inside the using block back out" is too strong. A gap
    ///     that holds a comment is not this key's gap, at either value.
    /// </remarks>
    const string Joined =
        "using System.Text;\n"
        + "using System.Globalization;\n"
        + "using Zeta.Support;\n"
        + "using Zeta.Support.Deep;\n"
        + "using static System.Math;\n"
        + "using static Zeta.Support.Helper;\n"
        + "using AliasOne = System.Collections.Generic.List<int>;\n"
        + "using AliasTwo = Zeta.Support.Helper;\n"
        + "using System.Threading;\n"
        + "\n"
        + "// a comment with a blank line above it\n"
        + "using Alpha.Widgets;\n"
        + "\n"
        + "namespace Skala.Probe;\n";

    /// <summary>
    ///     At <c>true</c>: exactly one blank line between groups, and exactly none within one.
    /// </summary>
    /// <remarks>
    ///     Read the seven gaps in order — they are the specification, and three of them refute the model
    ///     this key carried while it lived in <c>UsingsRule</c>:
    ///     <list type="number">
    ///         <item><c>System.Text</c> ▸ <c>System.Globalization</c> — same kind, same segment ⇒ 0.</item>
    ///         <item><c>System.Globalization</c> ▸ <c>Zeta.Support</c> — same kind, segment differs ⇒ 1.</item>
    ///         <item><c>Zeta.Support</c> ▸ <c>Zeta.Support.Deep</c> — the segment and nothing finer ⇒ 0.</item>
    ///         <item>
    ///             ⚠ <c>Zeta.Support.Deep</c> ▸ <c>static System.Math</c> ⇒ 1, and the two blank runs it sits
    ///             between show why: a change of <em>kind</em> separates on its own.
    ///         </item>
    ///         <item><c>static System.Math</c> ▸ <c>static Zeta.Support.Helper</c> — segment differs ⇒ 1.</item>
    ///         <item>
    ///             ⚠ <c>AliasOne = System…</c> ▸ <c>AliasTwo = Zeta…</c> ⇒ <b>0</b>, though their first
    ///             segments differ. Aliases are one group whatever they alias — the one place the segment is
    ///             not consulted.
    ///         </item>
    ///         <item>⚠ <c>AliasTwo</c> ▸ <c>System.Threading</c> ⇒ 1: kind again, alias back to plain.</item>
    ///     </list>
    /// </remarks>
    const string Separated =
        "using System.Text;\n"
        + "using System.Globalization;\n"
        + "\n"
        + "using Zeta.Support;\n"
        + "using Zeta.Support.Deep;\n"
        + "\n"
        + "using static System.Math;\n"
        + "\n"
        + "using static Zeta.Support.Helper;\n"
        + "\n"
        + "using AliasOne = System.Collections.Generic.List<int>;\n"
        + "using AliasTwo = Zeta.Support.Helper;\n"
        + "\n"
        + "using System.Threading;\n"
        + "\n"
        + "// a comment with a blank line above it\n"
        + "using Alpha.Widgets;\n"
        + "\n"
        + "namespace Skala.Probe;\n";

    static string Format(string source, params (string Key, string Value)[] overrides) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options)
            .Formatted.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("false", Joined)]
    [InlineData("true", Separated)]
    public void TheFormatterAlone_ReproducesTheOracleAtBothValues(string value, string expected) =>
        Assert.Equal(expected, Format(Probe, ("dotnet_separate_import_directive_groups", value)));

    /// <summary>A second run over the first run's output changes nothing, at either value.</summary>
    /// <remarks>
    ///     ⚠ Worth asserting rather than assuming, because this key both inserts and removes: a rule
    ///     that read the author's blank lines rather than deciding them outright would separate a group
    ///     again on every pass, and the failure would only appear on the second run.
    ///     <para>
    ///         ⚠ The seam SK-DIV-0074 is named for — <c>skala format</c> and <c>skala arrange</c> giving
    ///         different answers — is not asserted here and cannot be: it is closed structurally, by there
    ///         being one reader of the key rather than two. <c>ArrangementOptionTests</c> holds the other
    ///         half, which is that the arranger no longer acts on it at all.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("false")]
    [InlineData("true")]
    public void FormattingIsIdempotent_SoASecondRunAddsNothing(string value) {
        var once = Format(Probe, ("dotnet_separate_import_directive_groups", value));
        Assert.Equal(once, Format(once, ("dotnet_separate_import_directive_groups", value)));
    }

    /// <summary>
    ///     The anti-vacuity guard: the two expectations are genuinely two.
    /// </summary>
    [Fact]
    public void TheTwoValues_AreTwoOutputs() => Assert.NotEqual(Joined, Separated);
}
