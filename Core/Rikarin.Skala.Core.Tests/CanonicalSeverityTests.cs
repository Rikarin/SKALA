using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;

namespace Rikarin.Skala.Core.Tests;

/// <summary>
///     <c>SK9016</c>: what applying the canonical does to the severities the compiler runs at.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This exists because adopting the canonical took a repository from 0 build errors to 17 and
///         nothing said so.
///     </b> The canonical is the Rider export and the export carries 253
///     <c>dotnet_diagnostic.*.severity</c> lines, 213 of them <c>cs*</c>. Vixen carried 71 and not one
///     <c>cs*</c>. One of the 213 raises <c>CS9209</c> above the compiler's default; Vixen builds with
///     <c>TreatWarningsAsErrors</c>. The <c>.editorconfig</c> commit alone, touching no code, produced
///     17 errors in 15 files.
///     <para>
///         <c>SK9013</c> said nothing, because <c>dotnet_diagnostic</c> keys are deliberately outside the
///         option registry — so the loudest thing the canonical does to a repository was the one thing it
///         did silently.
///     </para>
/// </remarks>
public sealed class CanonicalSeverityTests {
    const string Path = "/repo/.editorconfig";

    static readonly CanonicalManifest Manifest = new("9.9.9", new string('a', 64), 0, 0);

    /// <summary>A canonical that raises one compiler diagnostic and one analyzer diagnostic.</summary>
    const string Canonical = """
                             root = true

                             [*]
                             dotnet_diagnostic.cs9209.severity = warning
                             dotnet_diagnostic.ca1852.severity = warning
                             """;

    static CanonicalStatus Describe(string existing) =>
        CanonicalSync.Describe(Path, exists: existing.Length > 0, existing, Manifest, Canonical);

    /// <summary>
    ///     The exact reported shape: the repository sets no <c>cs*</c> severity at all, and the
    ///     canonical sets 213 of them.
    /// </summary>
    [Fact]
    public void AFileThatSetsNoCompilerSeverity_IsToldWhichOnesTheCanonicalIntroduces() {
        var status = Describe(
            """
            [*]
            indent_size = 4
            """
        );

        var change = Assert.Single(status.SeverityChanges.Where(static change => change.Diagnostic == "CS9209"));

        Assert.Null(change.Before);
        Assert.Equal("warning", change.After);
        Assert.Equal(SeverityMove.Introduced, change.Move);
        Assert.True(change.IsCompilerDiagnostic);
        Assert.True(change.IsCSharp);
        Assert.True(change.CanBreakABuild);
    }

    /// <summary>
    ///     ⚠ Warning, and it is the only warning the canonical status produces. Drift is an error
    ///     because somebody edited a managed block; being behind is info because eighteen repositories
    ///     must not go red on a publication day. This is neither wrong nor survivable.
    /// </summary>
    [Fact]
    public void ABuildBreakingIntroduction_IsAWarningThatNamesTreatWarningsAsErrors() {
        var status = Describe("[*]\nindent_size = 4\n");

        var diagnostic = Assert.Single(
            status.Diagnostics.Where(static d =>
                d.Id == ConfigDiagnosticIds.CanonicalSeverityChange && d.Severity == SkalaSeverity.Warning
            )
        );

        Assert.Contains("CS9209", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("TreatWarningsAsErrors", diagnostic.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ An analyzer severity is reported and is <em>not</em> the headline. It adds and removes
    ///     findings; it does not fail a build before a single rule has run.
    /// </summary>
    [Fact]
    public void AnAnalyzerSeverity_IsReportedSeparatelyFromTheCompilerOnes() {
        var status = Describe("[*]\nindent_size = 4\n");

        var analyzer = Assert.Single(status.SeverityChanges.Where(static change => change.Diagnostic == "CA1852"));

        Assert.False(analyzer.IsCompilerDiagnostic);
        Assert.DoesNotContain(status.BuildBreaking, change => change.Diagnostic == "CA1852");
    }

    /// <summary>
    ///     ⚠ <b>Effective value against effective value, not block against block.</b> The local block
    ///     survives sync verbatim and comes <em>after</em> the canonical one, and editorconfig resolves
    ///     later sections over earlier ones — so a key the local block already pins does not change,
    ///     and reporting it as changing would be the one case where nothing happens. The first
    ///     implementation compared the two blocks and got this wrong.
    /// </summary>
    [Fact]
    public void AKeyTheLocalBlockAlreadyPins_DoesNotCount() {
        var status = Describe(
            """
            [*]
            dotnet_diagnostic.cs9209.severity = warning
            """
        );

        Assert.DoesNotContain(status.SeverityChanges, change => change.Diagnostic == "CS9209");
        Assert.Empty(status.BuildBreaking);
    }

    /// <summary>
    ///     ⚠ An unmanaged file's own <c>error</c> is <b>not</b> lowered by a canonical that says
    ///     <c>warning</c>, and this is the case a block-against-block comparison gets backwards. Sync
    ///     preserves the whole existing file below the local marker, and later sections win — so the
    ///     repository's stricter setting survives, and reporting it as a downgrade would send somebody
    ///     to defend a severity nothing was taking away.
    /// </summary>
    [Fact]
    public void AStricterLocalSeverity_SurvivesAndIsNotReportedAsLowered() {
        var status = Describe(
            """
            [*]
            dotnet_diagnostic.cs9209.severity = error
            """
        );

        Assert.DoesNotContain(status.SeverityChanges, change => change.Diagnostic == "CS9209");
    }

    /// <summary>
    ///     A genuine downgrade: a <em>managed</em> file whose canonical block sets <c>error</c> and
    ///     whose local block says nothing, against a newer canonical that says <c>warning</c>. That is
    ///     a canonical bump turning a severity down, which docs/plan/09 § "no-new-suppressions" calls
    ///     the widest suppression there is.
    /// </summary>
    [Fact]
    public void ACanonicalBumpThatTurnsASeverityDown_IsReportedAsLowered() {
        var managed = CanonicalLayout.Assemble(
            """
            root = true

            [*]
            dotnet_diagnostic.cs9209.severity = error
            """,
            "9.9.8",
            string.Empty
        );

        var status = CanonicalSync.Describe(Path, exists: true, managed, Manifest, Canonical);

        var change = Assert.Single(status.SeverityChanges.Where(static change => change.Diagnostic == "CS9209"));

        Assert.Equal(SeverityMove.Lowered, change.Move);
        Assert.Equal("error", change.Before);
        Assert.Equal("warning", change.After);
        Assert.False(change.CanBreakABuild);
    }

    /// <summary>
    ///     ⚠ The section is part of a severity's identity — docs/plan/09 § "no-new-suppressions" makes
    ///     that point for suppressions, and it applies here for the same reason: the same id under
    ///     <c>[Tools/**/*.cs]</c> and under <c>[*]</c> are different settings.
    /// </summary>
    [Fact]
    public void TheSameIdInADifferentSection_IsADifferentSetting() {
        var status = Describe(
            """
            [Tools/**/*.cs]
            dotnet_diagnostic.cs9209.severity = warning
            """
        );

        var change = Assert.Single(
            status.SeverityChanges.Where(static c => c.Diagnostic == "CS9209" && c.Section == "*")
        );

        Assert.Equal(SeverityMove.Introduced, change.Move);
    }

    /// <summary>
    ///     The real payload against a repository that carries none of it: nothing build-breaking moves.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         This assertion was inverted deliberately, and the inversion is the record of a product
    ///         decision.
    ///     </b> It read <c>csharp &gt; 200</c> — "the export carries 213" — and its summary called
    ///     the shipped payload "the case that broke Vixen", because a repository taking the canonical
    ///     inherited 213 <c>cs*</c> compiler severities and its build changed underneath it.
    ///     <para>
    ///         The export no longer sets any of them. Jiu rewrote <c>editor_config_template</c> to drop all
    ///         240 <c>dotnet_diagnostic.*</c> assignments — 213 <c>cs*</c>, 23 VB <c>bc*</c>, four
    ///         <c>ca*</c>/<c>wme</c>/<c>syslib</c> — so **Skala no longer ships compiler severities at all**
    ///         and adopting the canonical can no longer break a build on severity grounds.
    ///     </para>
    ///     <para>
    ///         ⚠ The assertion is <em>zero</em> rather than deleted, and that matters: a deleted test would
    ///         let the severities return unnoticed in a future export, which is exactly the regression that
    ///         made this file necessary. The reporting machinery it used to exercise is still covered —
    ///         the synthetic payloads above drive <c>Introduced</c>, <c>Raised</c> and the rest, including
    ///         <c>CS9209</c> — so what is retired here is the claim about the shipped payload, not the
    ///         instrument.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheShippedCanonical_MovesNoCompilerSeverities() {
        var status = CanonicalSync.Describe(
            Path,
            exists: true,
            "[*]\nindent_size = 4\n",
            CanonicalEditorConfig.Manifest,
            CanonicalEditorConfig.Text
        );

        var csharp = status.BuildBreaking.Where(static change => change.IsCSharp).ToArray();

        Assert.True(
            csharp.Length == 0,
            csharp.Length
            + " C# compiler severities move, and the export is supposed to carry none: "
            + string.Join(", ", csharp.Take(10).Select(static change => change.Diagnostic))
            + ".\n\nEither a `dotnet_diagnostic.*` assignment has come back into editor_config_template, "
            + "or the canonical was regenerated from an older export. Adopting the canonical must not "
            + "change a downstream build's diagnostics."
        );
    }
}
