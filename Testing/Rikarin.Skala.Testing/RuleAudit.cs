using Microsoft.CodeAnalysis;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The false-positive instrument: run every rule over a tree and print what it said, per rule.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/16 § R3's shipping bar is <b>zero false positives on the reference corpus</b>, and a
///     percentage cannot say whether that is met — only a list of findings a person read can. This
///     prints that list, grouped so that a rule with four findings is four lines to check and a rule
///     with four hundred is visibly not ready.
///     <para>
///         ⚠ It deliberately runs the <em>semantic</em> rules under a loose compilation, which the product
///         does not. The product's rule is right — a semantic rule that answers "no finding" because a
///         symbol did not resolve makes a clean report mean two things — but for an audit the asymmetry is
///         in the safe direction: every finding it produces is one to check, and the ones it misses are
///         misses rather than false positives. The count is therefore a floor, and it is labelled one.
///     </para>
/// </remarks>
public static class RuleAudit {
    public static string Run(IReadOnlyList<string> paths, bool semanticInLoose, bool implicitUsings = false) =>
        Report(paths, semanticInLoose, implicitUsings);

    /// <summary>
    ///     A stand-in for the <c>ImplicitUsings</c> file the SDK generates into <c>obj/</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         Without this, a tree that sets <c>ImplicitUsings</c> silences most of the semantic rule
    ///         set, and the silence looks like a clean result.
    ///     </b> The loose loader skips <c>obj/</c>, which
    ///     is where the generated global-usings file lives, so every <c>Dictionary&lt;,&gt;</c>,
    ///     <c>List&lt;&gt;</c> and <c>Task</c> in the tree binds to an error type and every rule that
    ///     asks a question about a type answers "no finding" for the wrong reason. Measured over Vixen:
    ///     195 724 errors without it and 128 833 with, <c>SK3002</c> 7 → 44, <c>SK8005</c> 0 → 25,
    ///     <c>SK1033</c> 0 → 5.
    ///     <para>
    ///         ⚠ It is generated here rather than committed as a fixture because docs/plan/15 § M7 records
    ///         exactly this file being used and never committed, which made M7's figures unreproducible from
    ///         the repository. A constant in the harness cannot go missing.
    ///     </para>
    ///     <para>
    ///         ⚠ Opt-in here, not automatic, because this audits an arbitrary tree. It is <em>on</em> by
    ///         default in <see cref="RuleCorpus" />, which sweeps the three vendored trees and nothing
    ///         else: for those, the compilation with the usings in it is the fair model and the one
    ///         without is the artefact — see that type's remarks for the two measurements that settled
    ///         it in opposite directions.
    ///     </para>
    /// </remarks>
    const string ImplicitUsings = RuleCorpus.ImplicitGlobalUsings;

    static string Report(IReadOnlyList<string> paths, bool semanticInLoose, bool implicitUsings) {
        var requested = paths;
        if (implicitUsings) {
            var directory = Path.Combine(Path.GetTempPath(), "skala-audit-usings");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "ImplicitGlobalUsings.cs");
            File.WriteAllText(file, ImplicitUsings);
            requested = [.. paths, file];
        }

        var loaded = ProjectLoader.Load(
            new LoadRequest {
                RepositoryRoot = Path.GetFullPath(requested.Count > 0 ? requested[0] : "."),
                Mode = LoadMode.Loose,
                Paths = requested
            }
        );

        if (loaded.IsEmpty) {
            return "nothing to audit.\n";
        }

        var builder = new StringBuilder();
        var findings = new List<Finding>();
        var errors = 0;

        foreach (var unit in loaded.Units) {
            var (options, _, _) = EditorConfigOptions.For(unit, loaded.Units[0].Compilation.AssemblyName ?? ".");
            var outcome = AnalyzerHost.Run(
                unit,
                options,

                [],

                // ⚠ The audit's whole trick: tell the host this is a binlog run even though the
                // compilation is loose, so the loose-mode filter does not remove the semantic rules.
                // It is the only place in the repository that does this, and the type's remarks say
                // why the asymmetry is in the safe direction.
                semanticInLoose ? LoadMode.Binlog : LoadMode.Loose,
                CancellationToken.None
            );

            findings.AddRange(outcome.Findings);
            errors += unit.Compilation.GetDiagnostics()
                .Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        var files = loaded.Units.Sum(static unit => unit.ReportablePaths.Count);
        builder.Append("audited ")
            .Append(files.ToString(CultureInfo.InvariantCulture))
            .Append(" file(s); the compilation has ")
            .Append(errors.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" error(s), so semantic findings are a floor rather than a count.");
        builder.AppendLine();

        var skala = findings
            .Where(static finding => finding.RuleId.StartsWith("SK", StringComparison.Ordinal))
            .ToList();

        if (skala.Count == 0) {
            builder.AppendLine("no Skala rule fired.");
            return builder.ToString();
        }

        foreach (var group in skala.GroupBy(static finding => finding.RuleId, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            builder.Append(group.Key)
                .Append("  ")
                .Append(group.Count().ToString(CultureInfo.InvariantCulture))
                .AppendLine(group.Count() == 1 ? " finding" : " findings");

            foreach (var finding in group
                         .OrderBy(static finding => finding.Path, StringComparer.Ordinal)
                         .ThenBy(static finding => finding.Line)) {
                builder.Append("    ")
                    .Append(Short(finding.Path))
                    .Append(':')
                    .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                    .Append("  ")
                    .AppendLine(finding.Message);
            }

            builder.AppendLine();
        }

        builder.AppendLine(VerifyFixes(loaded, skala));
        return builder.ToString();
    }

    /// <summary>
    ///     Applies every fix the audit produced and reports what stopped compiling.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the part that catches a rule being <em>wrong</em> rather than merely noisy. A
    ///     finding a person glances at looks fine; a fix that turns <c>x != null</c> into a pattern
    ///     inside an expression tree is CS8122 and cannot be glanced past. docs/plan/10: "A fixing tool
    ///     that can break the build is a tool an agent will use to break the build."
    /// </remarks>
    static string VerifyFixes(LoadedProject loaded, List<Finding> findings) {
        var byPath = findings
            .Where(static finding => finding.HasFix)
            .SelectMany(static finding => finding.Fix)
            .GroupBy(static edit => edit.Path, StringComparer.Ordinal)
            .ToList();

        if (byPath.Count == 0) {
            return "no fix to verify.";
        }

        var trees = loaded.Units[0].Compilation.SyntaxTrees.ToDictionary(
            static tree => Path.GetFullPath(tree.FilePath),
            static tree => tree,
            StringComparer.Ordinal
        );

        var before = loaded.Units[0].Compilation;
        var updated = before;
        var applied = 0;

        foreach (var group in byPath) {
            if (!trees.TryGetValue(group.Key, out var tree)) {
                continue;
            }

            var text = tree.GetText().ToString();
            foreach (var edit in group.OrderByDescending(static edit => edit.Start)) {
                if (edit.Start < 0 || edit.Start + edit.Length > text.Length) {
                    continue;
                }

                text = text[..edit.Start] + edit.Text + text[(edit.Start + edit.Length)..];
                applied++;
            }

            updated = updated.ReplaceSyntaxTree(
                tree,
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                    text,
                    (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)tree.Options,
                    tree.FilePath
                )
            );
        }

        var errorsBefore = Errors(before);
        var errorsAfter = Errors(updated);

        // ⚠ Counted per (file, diagnostic id), not per (file, line, id). SK1005's fix deletes the
        // namespace braces, so every pre-existing error in that file moves down a line; keyed on the
        // line, an unchanged error reads as a new one and the audit reports 73 regressions that are
        // all the same shrug. Per (file, id) is insensitive to a fix moving text and sensitive to a
        // fix breaking it, which is the question.
        var introduced = new List<string>();
        foreach (var entry in errorsAfter) {
            var was = errorsBefore.TryGetValue(entry.Key, out var previous) ? previous : 0;
            if (entry.Value > was) {
                introduced.Add($"{entry.Key} ×{(entry.Value - was).ToString(CultureInfo.InvariantCulture)}");
            }
        }

        var builder = new StringBuilder();
        builder.Append("applied ")
            .Append(applied.ToString(CultureInfo.InvariantCulture))
            .Append(" fix(es) across ")
            .Append(byPath.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" file(s): ")
            .Append(errorsBefore.Values.Sum().ToString(CultureInfo.InvariantCulture))
            .Append(" compiler error(s) before, ")
            .Append(errorsAfter.Values.Sum().ToString(CultureInfo.InvariantCulture))
            .Append(" after, ")
            .Append(introduced.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" (file, id) pair(s) worse than before.");

        foreach (var error in introduced.Take(20)) {
            builder.Append("    ").AppendLine(error);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Error counts per <c>(file, diagnostic id)</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not per <c>(file, line, id)</c>. SK1005's fix deletes the namespace braces, so every
    ///     pre-existing error in that file moves down a line; keyed on the line, an unchanged error
    ///     reads as a new one and the audit reports dozens of regressions that are all the same shrug.
    ///     Per <c>(file, id)</c> is insensitive to a fix <em>moving</em> text and sensitive to a fix
    ///     <em>breaking</em> it, which is the question being asked.
    /// </remarks>
    static Dictionary<string, int> Errors(Compilation compilation) {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var diagnostic in compilation.GetDiagnostics()) {
            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            var key = Short(diagnostic.Location.GetLineSpan().Path) + ":" + diagnostic.Id;
            result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return result;
    }

    static string Short(string path) {
        var parts = path.Split(Path.DirectorySeparatorChar);
        return parts.Length <= 3 ? path : string.Join('/', parts[^3..]);
    }
}
