using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Options;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Analysis;

/// <summary>What <c>skala fix</c> was asked to do.</summary>
public sealed record FixRequest {
    public IReadOnlyList<string> Paths { get; init; } = [];

    public string? RepositoryRoot { get; init; }

    /// <summary>
    ///     Null means auto: discover a workspace, or use loose mode when no target exists. Naming requires workspace.
    /// </summary>
    public LoadMode? Mode { get; init; }

    public string? BinlogPath { get; init; }

    public string? ProjectPath { get; init; }

    /// <summary>
    ///     ⚠ Only fixes the catalogue marks <c>fixIsSafe</c>. The default, and the only unqualified mode.
    /// </summary>
    public bool SafeOnly { get; init; } = true;

    /// <summary>
    ///     The rules an unsafe fix is applied for.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/10 § "Fixes": <c>skala fix</c> without <c>--safe</c> requires <c>--include</c>
    ///     explicitly. "An agent may do this; it must name the rules, which makes the choice visible in
    ///     its transcript."
    /// </remarks>
    public IReadOnlyList<string> Include { get; init; } = [];

    public bool DryRun { get; init; }

    public IReadOnlyList<string> Define { get; init; } = [];
}

/// <summary>
///     Applying fixes, and verifying every one of them.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/10: "Every applied fix is verified: re-parse, re-bind, diagnostic delta, revert on
///     regression. A fixing tool that can break the build is a tool an agent will use to break the
///     build." <see cref="FixSafety" /> is that re-bind. ⚠ Until #344 it was a re-parse *described* as
///     a re-bind — see that type's remarks for what the difference cost.
///     <para>
///         ⚠ The fixes are text edits and are applied back to front within a file, so that an earlier edit
///         cannot move a later one's offsets. Overlapping edits from two rules on one span are dropped
///         rather than merged — the second one's offsets are already wrong and applying it produces text
///         nobody wrote.
///     </para>
///     <para>
///         ⚠ Formatting runs after the fixes, over every file touched. A fix therefore does not have to
///         produce formatted text, which is what keeps the rules free of a second formatter.
///     </para>
/// </remarks>
public static class FixCommand {
    public static CommandResult Run(FixRequest request, CancellationToken cancellation = default) {
        var root = Path.GetFullPath(
            request.RepositoryRoot
            ?? FormatCommand.FindRepositoryRoot(request.Paths.Count > 0 ? request.Paths[0] : ".")
            ?? Directory.GetCurrentDirectory()
        );

        if (!request.SafeOnly && request.Include.Count == 0) {
            return new(
                ExitCodes.ConfigurationError,
                "skala fix: without --safe you must name the rules with --include SK1002,SK1024.\n"
                + "An unsafe fix changes shape enough to want eyes; naming it is what makes the choice visible.\n"
            );
        }

        if (request.SafeOnly
            && request.Include.Contains(
                Hosting.RoslynCodeStyle.NamingDiagnosticId,
                StringComparer.OrdinalIgnoreCase
            )) {
            return new(
                ExitCodes.ConfigurationError,
                "skala fix: IDE1006 is a solution-wide rename and is never part of --safe; "
                + "use `--include IDE1006` explicitly.\n"
            );
        }

        var namingRequested = request.Include.Contains(
            Hosting.RoslynCodeStyle.NamingDiagnosticId,
            StringComparer.OrdinalIgnoreCase
        );
        if (namingRequested && request.Mode is not null and not LoadMode.Workspace) {
            var selected = request.Mode.Value.ToString().ToLowerInvariant();
            return new(
                ExitCodes.ConfigurationError,
                $"skala fix: IDE1006 cannot use the explicitly selected --load={selected}; "
                + "omit --load or use --load workspace.\n"
            );
        }

        var mode = request.Mode
            ?? (namingRequested
                    ? LoadMode.Workspace
                    : ProjectLoader.ResolveAutoMode(
                        new LoadRequest {
                            RepositoryRoot = root,
                            Mode = LoadMode.Workspace,
                            ProjectPath = request.ProjectPath,
                            Paths = request.Paths
                        }
                    ));
        request = request with { Mode = mode };

        // ⚠ The compilations `check` built, kept so that every edit below can be re-bound in them.
        // Loading a second time would cost as much again as the analysis, which is the reason the
        // re-bind was skipped in the first place (#344).
        LoadedProject? loaded = null;
        var (checkResult, report) = CheckCommand.Run(
            new CheckRequest {
                Paths = request.Paths,
                RepositoryRoot = root,
                Mode = mode,
                BinlogPath = request.BinlogPath,
                ProjectPath = request.ProjectPath,
                AllowLoadFallback = mode == LoadMode.Binlog,
                IncludeFormatting = false,
                NoCache = true,
                Define = request.Define,
                Output = string.Empty,
                ObserveLoad = result => loaded = result
            },
            cancellation
        );
        if (checkResult.ExitCode == ExitCodes.LoadFailure) {
            return checkResult;
        }

        var naming = new NamingFixOutcome(0, []);
        if (namingRequested) {
            var namingPaths = report.Reportable
                .Where(static finding => finding.RuleId == Hosting.RoslynCodeStyle.NamingDiagnosticId)
                .Select(static finding => finding.Path)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (namingPaths.Length > 0) {
                naming = NamingFixCommand.Run(request, root, namingPaths, cancellation);
            }

            if (naming.Error is { } error) {
                return new(ExitCodes.ConfigurationError, $"skala fix: {error}.\n");
            }

            // Every ordinary fix below carries offsets into the pre-rename text. Re-run the check
            // after a written rename rather than applying a perfectly valid edit at a stale span.
            if (naming.Applied > 0 && !request.DryRun) {
                (_, report) = CheckCommand.Run(
                    new CheckRequest {
                        Paths = request.Paths,
                        RepositoryRoot = root,
                        Mode = mode,
                        BinlogPath = request.BinlogPath,
                        ProjectPath = request.ProjectPath,
                        AllowLoadFallback = false,
                        IncludeFormatting = false,
                        NoCache = true,
                        Define = request.Define,
                        Output = string.Empty,
                        ObserveLoad = result => loaded = result
                    },
                    cancellation
                );
            }
        }

        var applicable = report.Reportable.Where(finding => IsApplicable(finding, request)).ToList();
        if (applicable.Count == 0 && naming.Applied == 0) {
            var nothing = new StringBuilder();
            AppendSkippedNaming(nothing, naming);
            nothing.AppendLine(
                naming.Skipped.IsDefaultOrEmpty
                    ? "skala fix: nothing to apply."
                    : "skala fix: nothing else to apply."
            );
            return new(ExitCodes.Ok, nothing.ToString());
        }

        var output = new StringBuilder();
        AppendSkippedNaming(output, naming);
        var applied = naming.Applied;
        var reverted = 0;
        var unbound = 0;
        var safety = FixSafety.For(loaded);
        var changedFiles = naming.ChangedPaths.ToHashSet(StringComparer.Ordinal);

        // ⚠ Serial, deliberately, and the re-bind #344 added is affordable here.
        //
        // Measured on `fix Rules --include SK6034` over Skala — 54 files rewritten, far past what
        // `--safe` ever touches — before and after the re-bind: **user CPU 115 s and 118 s before,
        // 107-114 s over four runs after.** The cost of re-binding 54 documents does not clear the
        // noise floor of the analysis run that produced the findings. It is small because nothing is
        // rebuilt: `check` already bound this compilation, so the `before` model is nearly free, and
        // the `after` is one `ReplaceSyntaxTree` plus one document bind.
        //
        // ⚠ Parallelising this loop the way `FormatCommand` parallelises its own was tried and is
        // **not** an improvement: two runs at ten jobs took 189 s and 255 s of wall clock against
        // 56-144 s serial, at 46-62 % CPU and the same user CPU — waiting, not working. Every
        // `after` is a *different* derived compilation with its own declaration table over every
        // tree in the project, and ten of those alive at once cost more in GC than the parallelism
        // wins. ⚠ Both wall-clock figures were taken on a machine at load average ~300 from other
        // work, which is why the conclusion above rests on user CPU and not on them.
        foreach (var group in applicable
                     .SelectMany(static finding => finding.Fix.Select(edit => (finding, edit)))
                     .GroupBy(static pair => pair.edit.Path, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            var outcome = ApplyToFile(group.Key, [.. group], request, root, safety, cancellation);
            applied += outcome.Applied;
            if (outcome.Applied > 0 && !outcome.Reverted) {
                changedFiles.Add(group.Key);
            }

            reverted += outcome.Reverted ? 1 : 0;
            unbound += outcome.Check == FixCheck.Syntactic && outcome.Applied > 0 ? 1 : 0;
            if (outcome.Message.Length > 0) {
                output.Append(outcome.Message);
            }
        }

        if (applied > 0 && !request.DryRun) {
            // ⚠ Formatting last, over the files that changed. See the type's remarks.
            FormatCommand.Run(
                new FormatRequest {
                    Paths = changedFiles.Order(StringComparer.Ordinal).ToList(),
                    RepositoryRoot = root,
                    Quiet = true,
                    Define = request.Define
                }
            );
        }

        // ⚠ Said out loud rather than left to look like the other check. `--load=loose` has no
        // compilation to re-bind in, so these files were checked for parse errors and nothing else —
        // which is what every `skala fix` did before #344, and the sentence that used to be missing.
        if (unbound > 0) {
            output.Append("skala fix: ")
                .Append(unbound.ToString(CultureInfo.InvariantCulture))
                .Append(unbound == 1 ? " file was" : " file(s) were")
                .Append(" checked for parse errors only — ")
                .Append(
                    // ⚠ `loaded.Mode` and not `mode`: the ladder falls through, so asking for binlog
                    // on a machine with no binlog and no MSBuild lands in loose while `mode` still
                    // says binlog. Naming the mode that did not run is the fail-open this whole
                    // change is about, one level down.
                    loaded?.Mode == LoadMode.Loose
                        ? "--load=loose builds no compilation to re-bind against"
                        : "no loaded compilation holds it"
                )
                .AppendLine(", so a build is still owed.");
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
        return new(ExitCodes.Ok, output.ToString());
    }

    static void AppendSkippedNaming(StringBuilder output, NamingFixOutcome naming) {
        if (naming.Skipped.IsDefaultOrEmpty) {
            return;
        }

        output.Append("skala fix: skipped ")
            .Append(naming.Skipped.Length.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" IDE1006 rename(s) whose proposed names did not compile:");
        foreach (var skipped in naming.Skipped.Take(10)) {
            output.Append("  ").AppendLine(skipped);
        }

        if (naming.Skipped.Length > 10) {
            output.Append("  ... and ")
                .Append((naming.Skipped.Length - 10).ToString(CultureInfo.InvariantCulture))
                .AppendLine(" more");
        }
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

    readonly record struct FileOutcome(int Applied, bool Reverted, FixCheck Check, string Message);

    static FileOutcome ApplyToFile(
        string path,
        IReadOnlyList<(Finding Finding, FixEdit Edit)> pairs,
        FixRequest request,
        string root,
        FixSafety safety,
        CancellationToken cancellation
    ) {
        string original;
        try {
            original = File.ReadAllText(path);

            // ⚠ #353. `skala fix` is the fourth verb with this bug: `UnauthorizedAccessException`
            // does not derive from `IOException`, so an unreadable file crashed the command instead
            // of being reported as one file it could not fix. The handler already says exactly the
            // right thing — it just could not be reached for the commonest cause of a failed read.
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return new(0, false, FixCheck.Semantic, $"skala fix: {Relative(root, path)}: {exception.Message}\n");
        }

        var guard = TagGuard(path, original);

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

            // ⚠ `@formatter:off`. The finding still stands and is still reported — doc 09's
            // suppression mechanisms are the only four there are, and this is not a fifth. What the
            // tag forbids is the *rewrite*: report, never rewrite. An edit is dropped silently, the
            // same way an overlapping one is, and `skala check` goes on naming the line.
            if (guard.Touches(new Microsoft.CodeAnalysis.Text.TextSpan(edit.Start, edit.End - edit.Start))) {
                continue;
            }

            text = text[..edit.Start] + edit.Text + text[edit.End..];
            lastStart = edit.Start;
            applied++;
        }

        if (applied == 0) {
            return new(0, false, FixCheck.Semantic, string.Empty);
        }

        var verdict = safety.Verify(path, original, text, cancellation);

        // ⚠ Revert on regression. A fix that introduces a parse or bind error is a bug in the rule,
        // and the file is worth more than the finding.
        if (verdict.Threw is { } threw) {
            // ⚠ And revert when the check could not answer, for the reason ArrangementSafety gives:
            // an unanswered safety question is a revert, never a permission.
            return new(
                0,
                true,
                verdict.Check,
                $"skala fix: {Relative(root, path)} was reverted — the safety re-bind threw and could "
                + $"not say whether the fix was safe: {threw}. This is a Skala bug.\n"
            );
        }

        if (!verdict.Introduced.IsEmpty) {
            return new(
                0,
                true,
                verdict.Check,
                $"skala fix: {Relative(root, path)} was reverted — the fix introduced "
                + verdict.Introduced.Length.ToString(CultureInfo.InvariantCulture)
                + " new compiler diagnostic(s): "
                + string.Join(", ", verdict.Introduced.Take(3))
                + "\n"
            );
        }

        if (!request.DryRun) {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        return new(applied, false, verdict.Check, string.Empty);
    }

    /// <summary>
    ///     The <c>@formatter:off</c> regions of one file on disk.
    /// </summary>
    /// <remarks>
    ///     ⚠ Resolved per file rather than once per run, because the tags are <c>.editorconfig</c> keys
    ///     and a repository may spell them differently in one subtree. <see cref="ConfigurationCache" />
    ///     memoises the resolution on the sections that matched, so the cost is a dictionary hit for
    ///     every file after the first in a directory.
    /// </remarks>
    internal static FormatterTagGuard TagGuard(string path, string text) {
        FormattingOptions options;
        try {
            options = ConfigurationCache.Options(EditorConfigChain.For(path), null);
            // ⚠ #353 widened the filter to match what this comment already claimed. "A config the
            // fixer cannot read" is most often one it is not *permitted* to read, and that is the
            // one case the narrow catch let through — so the stated policy was implemented for
            // every cause of an unreadable config except the likeliest.
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // A config the fixer cannot read is not a reason to refuse the fix; it is the same
            // situation as no config at all, and the default has the tags on.
            return FormatterTagGuard.Open;
        }

        var tags = new PhaseOneOptions(options).Tags;
        if (!tags.Enabled) {
            return FormatterTagGuard.Open;
        }

        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            Microsoft.CodeAnalysis.Text.SourceText.From(text),
            CSharpFormatter.ParseOptions
        );

        return FormatterTagGuard.For(tree.GetRoot(), tags);
    }

    static string Relative(string root, string path) =>
        path.StartsWith(root, StringComparison.Ordinal)
            ? Path.GetRelativePath(root, path).Replace('\\', '/')
            : path;
}
