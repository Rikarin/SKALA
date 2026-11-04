using Microsoft.CodeAnalysis;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     <c>dotnet_diagnostic.SK0002.severity</c> for the findings no analyzer produces.
/// </summary>
/// <remarks>
///     ⚠ <c>SK0002</c>, <c>SK0003</c> and <c>SK7020</c> are built outside Roslyn's driver, so the severity
///     provider in <see cref="Hosting.EditorConfigOptions" /> never sees them: a scoped section meant what
///     <c>skala explain</c> says for every other rule and nothing for these. This answers the same question
///     from the same files, per path, with the same glob semantics, because it asks Roslyn's own
///     <see cref="AnalyzerConfigSet" />.
/// </remarks>
public sealed class ScopedSeverity {
    readonly Dictionary<string, AnalyzerConfig?> parsed = new(StringComparer.Ordinal);
    readonly Dictionary<string, AnalyzerConfigSet> sets = new(StringComparer.Ordinal);

    /// <summary>The severity a section sets for <paramref name="ruleId" /> at <paramref name="path" />.</summary>
    public ReportDiagnostic? Of(string ruleId, string path) {
        var directory = Path.GetDirectoryName(path);
        if (directory is null) {
            return null;
        }

        if (!sets.TryGetValue(directory, out var set)) {
            set = AnalyzerConfigSet.Create(Chain(directory));
            sets[directory] = set;
        }

        return set.GetOptionsForSourcePath(path).TreeOptions.TryGetValue(ruleId, out var severity) ? severity : null;
    }

    /// <summary>Whether a section switches <paramref name="ruleId" /> off at <paramref name="path" />.</summary>
    public bool IsOff(string ruleId, string path) => Of(ruleId, path) == ReportDiagnostic.Suppress;

    /// <summary><paramref name="findings" /> with each one's scoped severity, and without those switched off.</summary>
    public ImmutableArray<Finding> Apply(IEnumerable<Finding> findings) {
        var kept = ImmutableArray.CreateBuilder<Finding>();
        foreach (var finding in findings) {
            var severity = Of(finding.RuleId, finding.Path);
            if (severity != ReportDiagnostic.Suppress) {
                kept.Add(severity is { } set ? finding with { Severity = ToSkala(set, finding.Severity) } : finding);
            }
        }

        return kept.ToImmutable();
    }

    static SkalaSeverity ToSkala(ReportDiagnostic severity, SkalaSeverity fallback) =>
        severity switch {
            ReportDiagnostic.Error => SkalaSeverity.Error,
            ReportDiagnostic.Warn => SkalaSeverity.Warning,
            ReportDiagnostic.Info => SkalaSeverity.Info,
            ReportDiagnostic.Hidden => SkalaSeverity.Hidden,
            _ => fallback
        };

    /// <summary>The <c>.editorconfig</c> chain of a file in <paramref name="directory" />.</summary>
    ImmutableArray<AnalyzerConfig> Chain(string directory) {
        var chain = ImmutableArray.CreateBuilder<AnalyzerConfig>();
        foreach (var document in EditorConfigChain.For(Path.Combine(directory, "_.cs")).Documents) {
            if (!parsed.TryGetValue(document.Path, out var config)) {
                config = Read(document.Path);
                parsed[document.Path] = config;
            }

            if (config is not null) {
                chain.Add(config);
            }
        }

        return chain.ToImmutable();
    }

    static AnalyzerConfig? Read(string path) {
        try {
            return File.Exists(path) ? AnalyzerConfig.Parse(File.ReadAllText(path), path) : null;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return null; // As `EditorConfigOptions.For`: a config that cannot be read does not apply.
        }
    }
}
