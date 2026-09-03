using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     <c>alignment_tab_fill_style</c>: three spellings of one alignment column, under
///     <c>indent_style = tab</c>. SK-DIV-0032.
/// </summary>
/// <remarks>
///     ⚠ <b>The expectations are <c>jb cleanupcode</c> 2025.2.6's own bytes</b>, taken on
///     <see cref="Probe" /> under <c>OracleProfile.FormatOnly</c> with this repository's
///     <c>.editorconfig</c> plus <c>indent_style = tab</c>, <c>tab_width = 4</c> and the key at each of
///     its three values, one value per invocation. They are not derived from the rule in
///     <c>LayoutWriter.WriteIndentTo</c> — the rule was derived from them, and two of its three clauses
///     were wrong in the recorded model before the probe was run.
///     <para>
///         ⚠
///         <b>
///             This lives here rather than in <c>Testing/corpus/</c>, and that is a limitation and not a
///             preference.
///         </b> The corpus has no per-directory <c>.editorconfig</c>, so no committed fixture can
///         be tab-indented, so the key cannot carry an <c>oracle</c> glob and the key-flip sweep cannot
///         reach it — <c>verify skala_alignment_tab_fill_style</c> answers "no `oracle` fixture
///         in the registry" before and after this fix. Until that mechanism exists, this file is the whole
///         of the key's evidence, which is why it carries the oracle's output verbatim rather than an
///         assertion about it.
///     </para>
///     <para>
///         ⚠ Every probe that asked this key before was indented with <em>spaces</em>, at which all three
///         values spell the identical column and the key reads as inert. That is why it sat on
///         <c>PhaseOneOptions</c>' "never read by the C# formatter" list, and
///         <see cref="SpaceIndentation_IsUnmovedByTheKey" /> is the control that keeps the masking a
///         measured fact rather than an assumption.
///     </para>
/// </remarks>
public sealed class TabFillStyleTests {
    /// <summary>
    ///     One probe carrying both halves of the distinction the key turns on.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>var sum = alpha / + beta</c> is a plain continuation — a whole indent <em>level</em> — and
    ///     the three conditions below are alignment <em>columns</em>. The key moves the second and leaves
    ///     the first alone, which is the fact that separates "tabs to the block" from "tabs to the level"
    ///     and that a probe of alignment alone cannot see.
    ///     <para>
    ///         ⚠ The condition columns are 12, 14 and 19 on purpose. 12 is a multiple of the tab width and
    ///         14 and 19 are not, and <c>use_tabs_only</c> rounds 14 <em>down</em> while it rounds 19
    ///         <em>up</em> — the pair that refutes the "rounded down to a tab stop" model the entry carried
    ///         for three milestones.
    ///     </para>
    /// </remarks>
    const string Probe =
        "namespace Probe;\n"
        + "\n"
        + "public class Aligned {\n"
        + "\tpublic int M(int alpha, int beta, bool flag, object gate, object other) {\n"
        + "\t\tvar sum = alpha\n"
        + "\t\t+ beta;\n"
        + "\n"
        + "\t\tif (flag\n"
        + "\t\t&& alpha > 0) {\n"
        + "\t\t\treturn sum;\n"
        + "\t\t}\n"
        + "\n"
        + "\t\tlock (gate\n"
        + "\t\t?? other) {\n"
        + "\t\t\twhile (flag\n"
        + "\t\t\t&& beta > 0) {\n"
        + "\t\t\t\treturn sum;\n"
        + "\t\t\t}\n"
        + "\t\t}\n"
        + "\n"
        + "\t\treturn 0;\n"
        + "\t}\n"
        + "}\n";

    /// <summary>
    ///     <c>use_spaces</c> — <b>the export's own value</b>. Tabs to the line's level column, spaces for
    ///     the alignment remainder.
    /// </summary>
    /// <remarks>
    ///     ⚠ The continuation <c>+ beta</c> is three whole tabs and the condition <c>&amp;&amp; alpha</c>
    ///     is two tabs and four spaces, though both land on column 12. A level stays tabs; only what
    ///     alignment adds becomes spaces.
    /// </remarks>
    const string UseSpaces =
        "namespace Probe;\n"
        + "\n"
        + "public class Aligned {\n"
        + "\tpublic int M(int alpha, int beta, bool flag, object gate, object other) {\n"
        + "\t\tvar sum = alpha\n"
        + "\t\t\t+ beta;\n"
        + "\n"
        + "\t\tif (flag\n"
        + "\t\t    && alpha > 0) {\n"
        + "\t\t\treturn sum;\n"
        + "\t\t}\n"
        + "\n"
        + "\t\tlock (gate\n"
        + "\t\t      ?? other) {\n"
        + "\t\t\twhile (flag\n"
        + "\t\t\t       && beta > 0) {\n"
        + "\t\t\t\treturn sum;\n"
        + "\t\t\t}\n"
        + "\t\t}\n"
        + "\n"
        + "\t\treturn 0;\n"
        + "\t}\n"
        + "}\n";

    /// <summary>
    ///     <c>use_tabs_only</c>. The nearest tab stop, ties downwards, and no spaces at all — so the
    ///     column reached is not the column asked for, which is the option's own "(inaccurate)".
    /// </summary>
    /// <remarks>
    ///     ⚠ 14 goes down to 12 and 19 goes up to 20. A model that rounded down would put both short.
    /// </remarks>
    const string UseTabsOnly =
        "namespace Probe;\n"
        + "\n"
        + "public class Aligned {\n"
        + "\tpublic int M(int alpha, int beta, bool flag, object gate, object other) {\n"
        + "\t\tvar sum = alpha\n"
        + "\t\t\t+ beta;\n"
        + "\n"
        + "\t\tif (flag\n"
        + "\t\t\t&& alpha > 0) {\n"
        + "\t\t\treturn sum;\n"
        + "\t\t}\n"
        + "\n"
        + "\t\tlock (gate\n"
        + "\t\t\t?? other) {\n"
        + "\t\t\twhile (flag\n"
        + "\t\t\t\t\t&& beta > 0) {\n"
        + "\t\t\t\treturn sum;\n"
        + "\t\t\t}\n"
        + "\t\t}\n"
        + "\n"
        + "\t\treturn 0;\n"
        + "\t}\n"
        + "}\n";

    /// <summary>
    ///     <c>optimal_fill</c>: floor(column / tab width) tabs and spaces for the remainder. ⚠ What Skala
    ///     used to write at <em>every</em> value, under the name <c>use_spaces</c>.
    /// </summary>
    const string OptimalFill =
        "namespace Probe;\n"
        + "\n"
        + "public class Aligned {\n"
        + "\tpublic int M(int alpha, int beta, bool flag, object gate, object other) {\n"
        + "\t\tvar sum = alpha\n"
        + "\t\t\t+ beta;\n"
        + "\n"
        + "\t\tif (flag\n"
        + "\t\t\t&& alpha > 0) {\n"
        + "\t\t\treturn sum;\n"
        + "\t\t}\n"
        + "\n"
        + "\t\tlock (gate\n"
        + "\t\t\t  ?? other) {\n"
        + "\t\t\twhile (flag\n"
        + "\t\t\t\t   && beta > 0) {\n"
        + "\t\t\t\treturn sum;\n"
        + "\t\t\t}\n"
        + "\t\t}\n"
        + "\n"
        + "\t\treturn 0;\n"
        + "\t}\n"
        + "}\n";

    static string Format(string source, params (string Key, string Value)[] overrides) {
        var options = OptionResolver.Resolve(
            Path.Combine(Rikarin.Skala.Testing.Corpus.RepositoryRoot, "Test.cs"),
            [.. overrides.Select(static o => new KeyValuePair<string, string>(o.Key, o.Value))]
        ).Options;
        return CSharpFormatter.Format("Test.cs", SourceText.From(source), options).Formatted;
    }

    static string FormatWithTabs(string value) =>
        Format(
            Probe,
            ("indent_style", "tab"),
            ("tab_width", "4"),
            ("skala_alignment_tab_fill_style", value)
        );

    [Theory]
    [InlineData("use_spaces", UseSpaces)]
    [InlineData("use_tabs_only", UseTabsOnly)]
    [InlineData("optimal_fill", OptimalFill)]
    public void EachValue_SpellsTheColumnTheOracleSpells(string value, string expected) =>
        Assert.Equal(expected, FormatWithTabs(value));

    /// <summary>
    ///     ⚠ The three are genuinely three, and this is the anti-vacuity guard the theory above needs.
    /// </summary>
    /// <remarks>
    ///     An assertion that three values produce three expected strings passes trivially if two of the
    ///     expectations are the same string, and the divergence this fixes was exactly that shape: one
    ///     layout wearing three names.
    /// </remarks>
    [Fact]
    public void TheThreeValues_AreThreeDistinctLayouts() =>
        Assert.Equal(3, new HashSet<string>([UseSpaces, UseTabsOnly, OptimalFill], StringComparer.Ordinal).Count);

    /// <summary>
    ///     The control: with spaces for indentation the key changes nothing, at any value.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is why the key read as inert for three milestones, and it is asserted rather than
    ///     assumed. If <c>use_tabs_only</c>'s rounding ever escaped the tab guard it would move every
    ///     aligned line of every space-indented file — which is every file this repository ships — to a
    ///     column no configuration asked for, and no other test would see it.
    /// </remarks>
    [Fact]
    public void SpaceIndentation_IsUnmovedByTheKey() {
        var spaced = Probe.Replace("\t", "    ", StringComparison.Ordinal);
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in new[] { "use_spaces", "use_tabs_only", "optimal_fill" }) {
            outputs.Add(
                Format(spaced, ("indent_style", "space"), ("skala_alignment_tab_fill_style", value))
            );
        }

        Assert.Single(outputs);
        Assert.DoesNotContain('\t', outputs.Single());
    }

    /// <summary>
    ///     Every value is a fixed point: formatting the oracle's own output returns it unchanged.
    /// </summary>
    /// <remarks>
    ///     ⚠ An indentation rule that cannot re-read what it wrote is worse than one that writes the
    ///     wrong column, because the second run compounds it. <c>use_tabs_only</c> is the one at risk —
    ///     it deliberately lands on a column other than the one asked for, and a writer that measured
    ///     the written column rather than the requested one would drift a tab per pass.
    /// </remarks>
    [Theory]
    [InlineData("use_spaces", UseSpaces)]
    [InlineData("use_tabs_only", UseTabsOnly)]
    [InlineData("optimal_fill", OptimalFill)]
    public void EachValue_IsIdempotent(string value, string expected) =>
        Assert.Equal(
            expected,
            Format(
                expected,
                ("indent_style", "tab"),
                ("tab_width", "4"),
                ("skala_alignment_tab_fill_style", value)
            )
        );
}
