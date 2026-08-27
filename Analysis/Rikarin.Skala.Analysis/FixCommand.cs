using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis;

/// <summary>What <c>skala fix</c> was asked to do.</summary>
public sealed record FixRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    public LoadMode Mode { get; init; } = LoadMode.Loose;

    public string? BinlogPath { get; init; }

    /// <summary>⚠ Only fixes the catalogue marks <c>fixIsSafe</c>. The default, and the only unqualified mode.</summary>
    public bool SafeOnly { get; init; } = true;

    /// <summary>
    /// The rules an unsafe fix is applied for.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/10 § "Fixes": <c>skala fix</c> without <c>--safe</c> requires <c>--include</c>
    /// explicitly. "An agent may do this; it must name the rules, which makes the choice visible in
    /// its transcript."
    /// </remarks>
    public IReadOnlyList<string> Include { get; init; } = [];

    public bool DryRun { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];
}

/// <summary>
/// Applying fixes, and verifying every one of them.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/10: "Every applied fix is verified: re-parse, re-bind, diagnostic delta, revert on
/// regression. A fixing tool that can break the build is a tool an agent will use to break the
/// build." M5 implements re-parse and the diagnostic delta on the file's own tree; the cross-file
/// re-bind is M6's, with the compilation-wide gate that consumes it.
/// <para>
/// ⚠ The fixes are text edits and are applied back to front within a file, so that an earlier edit
/// cannot move a later one's offsets. Overlapping edits from two rules on one span are dropped
/// rather than merged — the second one's offsets are already wrong and applying it produces text
/// nobody wrote.
/// </para>
/// <para>
/// ⚠ Formatting runs after the fixes, over every file touched. A fix therefore does not have to
/// produce formatted text, which is what keeps the rules free of a second formatter.
/// </para>
/// </remarks>
public static class FixCommand {
    public static CommandResult Run(FixRequest request, CancellationToken cancellation = default) {
        var root = Path.GetFullPath(
            request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".")
            ?? Directory.GetCurrentDirectory()
        );

        if (!request.SafeOnly && request.Include.Count == 0) {
            return new CommandResult(
                ExitCodes.ConfigurationError,
                "skala fix: without --safe you must name the rules with --include SK1002,SK1024.\n"
                + "An unsafe fix changes shape enough to want eyes; naming it is what makes the choice visible.\n"
            );
        }

        var (_, report) = CheckCommand.Run(
            new CheckRequest {
                Paths = request.Paths,
                RepositoryRoot = root,
                Mode = request.Mode,
                BinlogPath = request.BinlogPath,
                IncludeFormatting = false,
                NoCache = true,
                Define = request.Define,
                Output = string.Empty
            },
            cancellation
        );

        var applicable = report.Reportable.Where(finding => IsApplicable(finding, request)).ToList();
        if (applicable.Count == 0) {
            return new CommandResult(ExitCodes.Ok, "skala fix: nothing to apply.\n");
        }

        var output = new StringBuilder();
        var applied = 0;
        var reverted = 0;

        foreach (var group in applicable
                     .SelectMany(static finding => finding.Fix.Select(edit => (finding, edit)))
                     .GroupBy(static pair => pair.edit.Path, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            var (count, wasReverted, message) = ApplyToFile(group.Key, [.. group], request, root);
            applied += count;
            reverted += wasReverted ? 1 : 0;
            if (message.Length > 0) {
                output.Append(message);
            }
        }

        if (applied > 0 && !request.DryRun) {
            // ⚠ Formatting last, over the files that changed. See the type's remarks.
            var files = applicable.SelectMany(static finding => finding.Fix.Select(static edit => edit.Path))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            FormatCommand.Run(
                new FormatRequest { Paths = files, RepositoryRoot = root, Quiet = true, Define = request.Define }
            );
        }

        output.Append("skala fix: applied ")
            .Append(applied.ToString(CultureInfo.InvariantCulture))
            .Append(applied == 1 ? " fix" : " fixes")
            .Append(request.DryRun ? " (dry run, nothing written)" : string.Empty);

        if (reverted > 0) {
            output.Append(", reverted ")
                .Append(reverted.ToString(CultureInfo.InvariantCulture))
                .Append(" file(s) that regressed");
        }

        output.AppendLine(".");
        return new CommandResult(ExitCodes.Ok, output.ToString());
    }

    static bool IsApplicable(Finding finding, FixRequest request) {
        if (!finding.HasFix) {
            return false;
        }

        if (request.Include.Count > 0) {
            return request.Include.Contains(finding.RuleId, StringComparer.OrdinalIgnoreCase);
        }

        return finding.FixIsSafe && RuleCatalog.Find(finding.RuleId) is { FixIsSafe: true };
    }

    static (int Applied, bool Reverted, string Message) ApplyToFile(
        string path,
        IReadOnlyList<(Finding Finding, FixEdit Edit)> pairs,
        FixRequest request,
        string root
    ) {
        string original;
        try {
            original = File.ReadAllText(path);
        } catch (IOException exception) {
            return (0, false, $"skala fix: {Relative(root, path)}: {exception.Message}\n");
        }

        var before = Diagnostics(original);

        // Back to front, so that an earlier edit cannot move a later one's offsets.
        var ordered = pairs.OrderByDescending(static pair => pair.Edit.Start).ToList();
        var text = original;
        var applied = 0;
        var lastStart = int.MaxValue;

        foreach (var (_, edit) in ordered) {
            if (edit.End > lastStart || edit.Start < 0 || edit.End > text.Length) {
                // ⚠ Overlaps are dropped, not merged. The next run picks the dropped one up against
                // text whose offsets are correct.
                continue;
            }

            text = text[..edit.Start] + edit.Text + text[edit.End..];
            lastStart = edit.Start;
            applied++;
        }

        if (applied == 0) {
            return (0, false, string.Empty);
        }

        var after = Diagnostics(text);
        if (after.Length > before.Length) {
            // ⚠ Revert on regression. A fix that introduces a parse or bind error is a bug in the
            // rule, and the file is worth more than the finding.
            return (
                0,
                true,
                $"skala fix: {Relative(root, path)} was reverted — the fix introduced "
                + (after.Length - before.Length).ToString(CultureInfo.InvariantCulture)
                + " new compiler diagnostic(s): "
                + string.Join(", ", after.Except(before, StringComparer.Ordinal).Take(3))
                + "\n"
            );
        }

        if (!request.DryRun) {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        return (applied, false, string.Empty);
    }

    /// <summary>
    /// The file's own syntactic diagnostics, as the before/after delta.
    /// </summary>
    /// <remarks>
    /// ⚠ Syntactic only, on purpose. A semantic re-bind needs the whole compilation rebuilt per
    /// file, which turns a fix pass over fifty files into a minute; the syntactic delta catches
    /// every fix that produced text that is not C#, which is the failure class a text-edit fix can
    /// actually have. The compilation-wide re-bind is M6's, beside the gate that wants it anyway.
    /// </remarks>
    static ImmutableArray<string> Diagnostics(string text) {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            Microsoft.CodeAnalysis.Text.SourceText.From(text),
            CSharpFormatter.ParseOptions
        );

        return [
            .. tree.GetDiagnostics()
                .Where(static diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .Select(static diagnostic => diagnostic.Id
                    + "@"
                    + diagnostic.Location.SourceSpan.Start.ToString(CultureInfo.InvariantCulture)
                )
        ];
    }

    static string Relative(string root, string path) =>
        path.StartsWith(root, StringComparison.Ordinal)
            ? Path.GetRelativePath(root, path).Replace('\\', '/')
            : path;
}
