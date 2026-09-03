using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Tests;

/// <summary>
///     Every option in the registry, at a legal value and at an illegal one.
/// </summary>
/// <remarks>
///     ⚠ <b>The negative half is the point.</b> Until M9 the suite had positive coverage only —
///     <c>OptionCoverageTests.EveryImplementedOption_ChangesTheOutputOfItsCorpusFile</c> feeds every
///     enum member and asserts the resolution reports no value error. Nothing anywhere fed an illegal
///     value, so "the tool refuses what it should refuse" was never a claim the suite made, and it was
///     false for 83 of the 520 options: 27 typed <c>string</c> and validating nothing, 56 typed
///     <c>int</c> with nothing behind them but <c>int.TryParse</c>.
///     <para>
///         ⚠ Driven by the registry rather than by a list, so it cannot fall behind it. A new option
///         is covered the moment it is added, and a new <c>string</c> option fails
///         <see cref="FreeFormOptions_AreTheReviewedList" /> until somebody writes down why every string
///         is legal for it.
///     </para>
/// </remarks>
public sealed class OptionValueValidationTests {
    /// <summary>
    ///     The options for which every string really is a legal value, and therefore nothing can be
    ///     refused. ⚠ Hard-coded on purpose: this is the exemption list, and an exemption that lives in
    ///     a <c>default:</c> switch arm is an exemption nobody reviews.
    /// </summary>
    static readonly string[] FreeForm = [
        // Roslyn accepts any token `SyntaxFacts` calls a keyword, which moves with the compiler.
        "csharp_preferred_modifier_order",

        // The header text itself.
        "file_header_template",

        // The literal text of the formatter's off/on marker comments.
        "skala_formatter_off_tag", "skala_formatter_on_tag",

        // ⚠ A genuine gap rather than a free-form value: JetBrains documents the key nowhere, so the
        // domain is unknown rather than open. Recorded as such on the entry, and the reason names
        // what would have to be published to close it.
        //
        // ⚠ `resharper_labeled_statement_style` was the second of these and is deleted — nothing in
        // production code read it under any spelling and no oracle fixture pinned it. Leaving it here
        // did not fail this list; it inflated `FreeForm.Length` by one and quietly broke the
        // *count* assertion at the bottom, which is the half of this test that is not vacuous.
        "skala_prefer_wrap_around_eq",

        // A comma-separated list of XML documentation tag names.
        "skala_xmldoc_linebreak_before_elements"
    ];

    [Fact]
    public void FreeFormOptions_AreTheReviewedList() {
        var actual = OptionRegistry.All
            .Where(OptionDomain.IsFreeForm)
            .Select(static info => info.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(FreeForm.Order(StringComparer.Ordinal).ToArray(), actual);

        // ⚠ And each one says why. `type: string` is a claim that every value is legal; 27 options
        // carried it because the distillation typed them that way and nobody wrote the claim down,
        // so nobody could disagree with it.
        foreach (var info in OptionRegistry.All.Where(OptionDomain.IsFreeForm)) {
            Assert.True(
                info.FreeFormBecause is { Length: > 40 },
                $"{info.Key} accepts every string and gives no reason worth reading."
            );
        }
    }

    [Fact]
    public void EveryOption_AcceptsEveryValueInItsDomain() {
        var failures = new List<string>();
        var covered = 0;

        foreach (var info in OptionRegistry.All) {
            covered++;
            foreach (var value in OptionDomain.EveryLegalValue(info)) {
                var resolution = Resolve(info.Key, value);
                foreach (var error in resolution.ValueErrors) {
                    failures.Add($"{info.Key} = {value}: refused — {error.Reason}");
                }
            }
        }

        Assert.Equal(OptionRegistry.Count, covered);
        Assert.True(
            failures.Count == 0,
            "The registry refuses values its own domain contains:\n  " + string.Join("\n  ", failures)
        );
    }

    /// <summary>
    ///     ⚠ The assertion this whole change exists for.
    /// </summary>
    /// <remarks>
    ///     Three claims, and each was false somewhere before M9: the value is refused rather than
    ///     applied; the refusal reaches a diagnostic instead of an unread field on
    ///     <see cref="ResolutionResult" />; and the message names what is in force instead, which is the
    ///     only part a reader can act on.
    /// </remarks>
    [Fact]
    public void EveryOptionWithAClosedDomain_RefusesAValueOutsideIt() {
        var failures = new List<string>();
        var covered = 0;

        foreach (var info in OptionRegistry.All) {
            foreach (var value in OptionDomain.IllegalValues(info)) {
                covered++;
                var resolution = Resolve(info.Key, value);
                var diagnostics = ConfigurationAnalyzer.Analyze(resolution)
                    .Where(static d => d.Id == ConfigDiagnosticIds.OptionValueOutOfDomain)
                    .ToArray();

                if (diagnostics.Length != 1) {
                    failures.Add($"{info.Key} = {value}: {diagnostics.Length} SK9017, expected exactly one");
                    continue;
                }

                var diagnostic = diagnostics[0];
                var effective = resolution.Options.GetText(info.Id);

                if (!resolution[info.Id].IsDefault) {
                    failures.Add($"{info.Key} = {value}: refused and applied anyway");
                }

                if (resolution[info.Id].Refused is null) {
                    failures.Add($"{info.Key} = {value}: the refusal is not on the resolved option");
                }

                if (!diagnostic.Message.Contains(value, StringComparison.Ordinal)) {
                    failures.Add($"{info.Key} = {value}: the message does not quote the value");
                }

                if (!diagnostic.Message.Contains($"'{effective}' is in force", StringComparison.Ordinal)) {
                    failures.Add(
                        $"{info.Key} = {value}: the message does not name the fallback ('{effective}'): {diagnostic.Message}"
                    );
                }

                if (diagnostic.Severity != SkalaSeverity.Warning) {
                    failures.Add($"{info.Key} = {value}: severity is {diagnostic.Severity}, not warning");
                }
            }
        }

        // ⚠ Both directions. A test that only checks the failures it found would pass just as
        // happily over an empty sweep, which is how a registry-driven guard goes quiet.
        var closed = OptionRegistry.All.Count(static info => !OptionDomain.IsFreeForm(info));
        Assert.Equal(OptionRegistry.Count - FreeForm.Length, closed);
        Assert.True(covered >= closed, $"only {covered} illegal values were tried across {closed} options");

        Assert.True(
            failures.Count == 0,
            "An out-of-domain value was not reported as SK9017:\n  " + string.Join("\n  ", failures)
        );
    }

    /// <summary>
    ///     <c>indent_size = tab</c> is spec-legal and means "the width <c>tab_width</c> carries".
    /// </summary>
    /// <remarks>
    ///     The EditorConfig specification: "If this equals <c>tab</c>, the <c>indent_size</c> shall be
    ///     set to the tab size, which should be <c>tab_width</c> (if specified)". Both reference cores —
    ///     editorconfig-core-c and editorconfig-core-net — implement exactly that. Skala typed the key
    ///     <c>int</c> and refused a conformant file, in silence.
    ///     <para>
    ///         ⚠ It has to survive expansion too: <c>indent_size</c> is one of ReSharper's generalized
    ///         keys, and the keys it names take a number, so propagating the literal <c>tab</c> would
    ///         leave every one of them at its own default.
    ///     </para>
    /// </remarks>
    [Fact]
    public void IndentSizeTab_TakesItsWidthFromTabWidth_AndPropagates() {
        var resolution = Resolve(
            """
            root = true
            [*]
            indent_size = tab
            tab_width = 3
            """
        );

        Assert.Empty(resolution.ValueErrors);

        // What the file says is what provenance reports; what it resolved to is the width.
        Assert.Equal("tab", resolution[OptionId.IndentSize].Value);
        Assert.Equal(3, resolution.Options.GetInt(OptionId.IndentSize));
        Assert.Equal(3, resolution.Options.GetInt(OptionId.SkalaIndentSize));

        // ⚠ Ordering must not matter, and it is the one thing that would: options are applied in
        // ordinal key order and `indent_size` sorts before `tab_width`.
        var reversed = Resolve(
            """
            root = true
            [*]
            tab_width = 3
            indent_size = tab
            """
        );
        Assert.Equal(3, reversed.Options.GetInt(OptionId.IndentSize));

        // With no tab_width, the registry default stands in — the spec's "else, the tab size set by
        // the editor", and Skala's editor-equivalent is the default.
        var alone = Resolve("indent_size", "tab");
        Assert.Empty(alone.ValueErrors);
        Assert.Equal(4, alone.Options.GetInt(OptionId.IndentSize));

        // ⚠ And only on the specification's own key. JetBrains documents `skala_indent_size`
        // as "an integer" and says nothing about `tab`, so accepting it there would be an invention.
        var reSharper = Resolve("skala_indent_size", "tab");
        var error = Assert.Single(reSharper.ValueErrors);
        Assert.Contains("expected an integer >= 1", error.Reason, StringComparison.Ordinal);
    }

    static ResolutionResult Resolve(string key, string value) =>
        Resolve($"root = true{Environment.NewLine}[*]{Environment.NewLine}{key} = {value}");

    static ResolutionResult Resolve(string text) {
        var directory = Path.Combine(Path.GetTempPath(), "skala-option-domain");
        return OptionResolver.Resolve(
            EditorConfigChain.Of(
                Path.Combine(directory, "Probe.cs"),
                EditorConfigDocument.FromText(Path.Combine(directory, ".editorconfig"), text)
            )
        );
    }
}
