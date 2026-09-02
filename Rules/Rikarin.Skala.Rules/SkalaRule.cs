using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules;

/// <summary>
///     The bridge between <c>rules.json</c> and Roslyn's <see cref="DiagnosticDescriptor" />.
/// </summary>
/// <remarks>
///     ⚠ ADR-006: Skala's rules are ordinary <see cref="DiagnosticAnalyzer" />s, so they run inside
///     <c>csc</c> and inside Rider unchanged, <c>TreatWarningsAsErrors</c> works on them, and
///     <c>dotnet_diagnostic.SK1010.severity</c> configures them for free. Nothing here may hand-write a
///     descriptor: the title, the category and the default severity come from the catalogue, so the
///     analyzer, the docs page, <c>skala explain</c> and the SARIF <c>rules[]</c> block cannot disagree.
/// </remarks>
public static class SkalaRule {
    static readonly Dictionary<string, DiagnosticDescriptor> Descriptors = Build();

    /// <summary>The descriptor for a rule id, built once from the catalogue.</summary>
    public static DiagnosticDescriptor Descriptor(string id) => Descriptors[id];

    /// <summary>Every descriptor, for <see cref="DiagnosticAnalyzer.SupportedDiagnostics" />.</summary>
    public static ImmutableArray<DiagnosticDescriptor> All { get; } = BuildAll();

    static ImmutableArray<DiagnosticDescriptor> BuildAll() {
        var builder = ImmutableArray.CreateBuilder<DiagnosticDescriptor>();
        foreach (var rule in RuleCatalog.All) {
            builder.Add(Descriptors[rule.Id]);
        }

        return builder.ToImmutable();
    }

    static Dictionary<string, DiagnosticDescriptor> Build() {
        var result = new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal);
        foreach (var rule in RuleCatalog.All) {
            result[rule.Id] = new(
                rule.Id,
                rule.Title,
                "{0}",
                "Skala." + rule.Category,
                Severity(rule.DefaultSeverity),
                !rule.Retired && rule.DefaultSeverity != RuleSeverity.None,
                rule.Summary,
                "https://github.com/Rikarin/Skala/blob/main/docs/rules/" + rule.Id + ".md",
                rule.HasFix ? new[] { "Fixable" } : Array.Empty<string>()
            );
        }

        return result;
    }

    static DiagnosticSeverity Severity(RuleSeverity severity) =>
        severity switch {
            RuleSeverity.Error => DiagnosticSeverity.Error,
            RuleSeverity.Warning => DiagnosticSeverity.Warning,
            RuleSeverity.Suggestion => DiagnosticSeverity.Info,
            _ => DiagnosticSeverity.Hidden
        };

    /// <summary>
    ///     Whether the compilation's <em>effective</em> language version reaches a rule's floor.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/08: "checked against the compilation's effective LangVersion, not the SDK's". A
    ///     rule that suggests C# 12 syntax to a project pinned at C# 10 produces a fix that does not
    ///     compile, and an agent that applies it has broken the build on the tool's advice.
    ///     <para>
    ///         <see cref="LanguageVersion.Latest" />, <c>LatestMajor</c>, <c>Preview</c> and <c>Default</c>
    ///         are mapped through <c>MapSpecifiedToEffectiveVersion</c> by Roslyn before the compilation
    ///         exists, so the value read here is always a concrete version.
    ///     </para>
    /// </remarks>
    public static bool MeetsLanguageVersion(Compilation compilation, string? floor) {
        if (floor is null) {
            return true;
        }

        if (compilation is not CSharpCompilation csharp) {
            return false;
        }

        return !(csharp.LanguageVersion < Parse(floor));
    }

    /// <summary>
    ///     Maps a rule's declared <c>languageVersion</c> onto the Roslyn version it means.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Public, and the <c>Try</c> shape, so that a test can ask the question the switch
    ///     cannot ask about itself</b> — <c>RuleCatalogTests.EveryDeclaredLanguageVersion_IsRecognised</c>
    ///     walks every distinct non-null <c>languageVersion</c> in <c>rules.json</c> through here and
    ///     names the value it could not map. Nothing asserted that before, and the table shipped
    ///     without a <c>"6.0"</c> arm while <c>SK1061</c> declared <c>6.0</c> as its floor (#296).
    /// </remarks>
    public static bool TryParseLanguageVersion(string floor, out LanguageVersion version) {
        switch (floor) {
            case "6.0": version = LanguageVersion.CSharp6; return true;
            case "7.0": version = LanguageVersion.CSharp7; return true;
            case "7.1": version = LanguageVersion.CSharp7_1; return true;
            case "7.2": version = LanguageVersion.CSharp7_2; return true;
            case "7.3": version = LanguageVersion.CSharp7_3; return true;
            case "8.0": version = LanguageVersion.CSharp8; return true;
            case "9.0": version = LanguageVersion.CSharp9; return true;
            case "10.0": version = LanguageVersion.CSharp10; return true;
            case "11.0": version = LanguageVersion.CSharp11; return true;
            case "12.0": version = LanguageVersion.CSharp12; return true;
            case "13.0": version = LanguageVersion.CSharp13; return true;
            case "14.0": version = LanguageVersion.CSharp14; return true;
            default: version = LanguageVersion.Default; return false;
        }
    }

    /// <summary>
    ///     ⚠ An unrecognised floor over-fires. It used to under-fire, which is worse.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The old fallback was <see cref="LanguageVersion.Preview" />, so a floor the table did
    ///     not name silenced its rule on every real project rather than on none.</b> The rule was still
    ///     registered, still in the SARIF <c>rules[]</c>, still in <c>docs/rules/</c>, and reported
    ///     nothing anywhere — one typo in <c>rules.json</c> and a rule is dead with no error (#296).
    ///     <para>
    ///         ⚠ <b>The fallback is <see cref="LanguageVersion.Default" /> rather than a throw, and the
    ///         reason is this batch's own lesson.</b> <c>Parse</c> runs inside an analyzer callback, so
    ///         a throw here is not a loud failure — Roslyn catches it, reports <c>AD0001</c>, and drops
    ///         the analyzer for the rest of the compilation, which silences the rule *and* every other
    ///         rule that analyzer hosts while the run still reports success (#315, #298, #295).
    ///         Throwing would have swapped a quiet under-fire for a quieter one. <c>Default</c> is 0,
    ///         below every concrete version a compilation reports, so the floor is met everywhere and
    ///         the rule fires where it does not belong — noisy, attributable, and noticed.
    ///     </para>
    ///     <para>
    ///         The loud failure belongs at build time instead, where it can be loud:
    ///         <c>EveryDeclaredLanguageVersion_IsRecognised</c> is a red test naming the value, and it
    ///         is the half of #296 that matters, since it fires before anything ships.
    ///     </para>
    /// </remarks>
    static LanguageVersion Parse(string floor) =>
        TryParseLanguageVersion(floor, out var version) ? version : LanguageVersion.Default;
}

/// <summary>
///     The edits that fix a finding, carried on the diagnostic itself.
/// </summary>
/// <remarks>
///     ⚠ Deliberately not a <c>CodeFixProvider</c>. Doc 09 wants SARIF results to carry
///     <c>fixes[].artifactChanges</c> — "real, applicable edits, not prose" — and ADR-005 already says
///     Skala's output is a minimal text-edit list against the original <c>SourceText</c>. A text edit
///     serialises into SARIF directly, applies without a <c>Workspace</c>, and keeps
///     <c>Rikarin.Skala.Rules</c> free of the workspace layer that ADR-006's "loads into csc and into
///     Rider" makes expensive. A <c>CodeFixProvider</c> wrapper over the same edits is an IDE
///     convenience and can be added later without changing anything here.
///     <para>
///         ⚠ The edits do not have to produce formatted text. <c>skala fix</c> runs the formatter over
///         every file it touched, so a fix may leave a brace at column 0 and the pipeline repairs it. A
///         fix that tried to be its own formatter would be a second formatter.
///     </para>
/// </remarks>
public static class FixEdits {
    public const string CountKey = "skala.fix.count";

    public static string StartKey(int index) => "skala.fix." + index + ".start";

    public static string LengthKey(int index) => "skala.fix." + index + ".length";

    public static string TextKey(int index) => "skala.fix." + index + ".text";

    /// <summary>Packs one or more replacements into a diagnostic's property bag.</summary>
    public static ImmutableDictionary<string, string?> Pack(params (TextSpan Span, string Text)[] edits) {
        var builder = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        builder[CountKey] = edits.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < edits.Length; i++) {
            builder[StartKey(i)] = edits[i].Span.Start.ToString(System.Globalization.CultureInfo.InvariantCulture);
            builder[LengthKey(i)] = edits[i].Span.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            builder[TextKey(i)] = edits[i].Text;
        }

        return builder.ToImmutable();
    }
}
