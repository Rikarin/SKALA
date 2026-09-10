using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Reporting;

/// <summary>The surfaces a <see cref="RunReport" /> can be rendered to.</summary>
public enum ReportFormat {
    /// <summary>Default TTY output, grouped by file.</summary>
    Terminal,

    /// <summary><c>path:line:col: level SKxxxx: message</c> — greppable, and every editor parses it.</summary>
    Plain,

    /// <summary>The SARIF, verbatim.</summary>
    Json,

    /// <summary>GitHub Actions annotations plus a step-summary table.</summary>
    Github,

    /// <summary>The three-bucket agent report (docs/plan/10).</summary>
    Agent,

    /// <summary><c>skala report --format=markdown</c> — a PR comment.</summary>
    Markdown,

    /// <summary>JUnit XML, for a CI system that only knows how to render tests.</summary>
    JUnit
}

/// <summary>
///     What took a file out of a run — the distinction the <c>INCOMPLETE</c> banner exists to draw.
/// </summary>
/// <remarks>
///     ⚠ #355. The banner #345 added said "this is a Skala bug" for every blocking diagnostic, and
///     <c>SK9015</c> is not one: a mode-600 file owned by someone else, a half-restored package cache
///     or a network mount mid-reconnect are conditions the tool handled correctly, and the fix is
///     <c>chmod</c>. The sentence is the one thing that stops an agent acting on a bad verdict, and a
///     sentence that sends the reader to the wrong place spends that credibility on nothing.
///     <para>
///         Declared in order of strength: a file carrying more than one blocking diagnostic is
///         attributed to the lowest value.
///     </para>
///     <para>
///         ⚠ #360. The last member is not a file cause at all. <c>SK9028</c> at error severity sits
///         at the baseline's path, and until #360 it fell through <see cref="Renderer.CauseOf" />'s
///         default and into <see cref="Renderer.BlockedFiles" />, so a one-file tree with a
///         merge-conflict marker in <c>.skala/baseline.sarif</c> read "1 of 1 file was not checked
///         — this is a Skala bug". The source file <em>was</em> checked; it was the baseline that
///         could not be read, and a conflict marker in a committed file is the repository's condition.
///         A gate input is never in the set of files being checked, so it is never in the fraction.
///     </para>
///     <para>
///         ⚠ #361. Neither is a project. <c>SK9024</c>/<c>SK9029</c> at error severity sit at the
///         <c>.csproj</c> or <c>.slnx</c> the workspace rung could not load, and the binlog ladder
///         carries them into the loose report it falls through to — so the same one-file tree with a
///         <c>.csproj</c> naming an SDK that does not exist read "1 of 1 file was not checked — this
///         is a Skala bug" above <b>exit 0</b>, the "1 file" being the project. <see cref="LoadRung" />
///         is the sibling of <see cref="GateInput" />: outside the fraction, its own sentence, and —
///         since #361 — a reliability failure at the gate.
///     </para>
/// </remarks>
public enum IncompleteCause {
    /// <summary>
    ///     Skala's own fault — <c>SK9098</c>, <c>SK9096</c>, <c>SK9095</c>, <c>SK9099</c>, and any id
    ///     not listed below.
    /// </summary>
    Defect,

    /// <summary>The file could not be read (<c>SK9015</c>). An environment condition, not a defect.</summary>
    Unreadable,

    /// <summary>The file does not parse (<c>SK9010</c>). Left byte-identical under ADR-003.</summary>
    Unparseable,

    /// <summary>
    ///     An input the gate compares against could not be read (<c>SK9028</c> at error severity):
    ///     a baseline that exists and will not open, or a <c>--since</c> reference that will not
    ///     resolve. Not a source file, so never counted among the files that were not checked;
    ///     <see cref="Renderer.GateInputs" /> is where the banner reads it from.
    /// </summary>
    GateInput,

    /// <summary>
    ///     A project or solution the load ladder found and could not load (<c>SK9024</c> or
    ///     <c>SK9029</c> at error severity), after which the run fell back to the syntactic rules.
    ///     Not a source file, so never counted among the files that were not checked;
    ///     <see cref="Renderer.OutsideTheFraction" /> is where the banner reads it from.
    /// </summary>
    LoadRung
}

/// <summary>
///     The two tool-diagnostic ids the renderers must recognise by name.
/// </summary>
/// <remarks>
///     ⚠ Mirrors of <c>FormatDiagnosticIds.FileIoFailed</c> and <c>FormatDiagnosticIds.NotParseable</c>,
///     not new allocations. This assembly sits below the formatter on purpose — a renderer that could
///     reach formatting code is a renderer that can be tempted to run some — so it cannot reference
///     the originals, and <c>ToolDiagnosticIdTests</c> forbids a bare literal. The constants keep the
///     originals' names so that the one-id-one-concept check reads them as the same concept, which
///     they are. The other ids the classifier names — <c>SK9028</c>, <c>SK9024</c>, <c>SK9029</c> —
///     are read straight off <c>ConfigDiagnosticIds</c>, which lives in Core and which this assembly
///     already references. Nothing else in the <c>SK9xxx</c> range is named here: every other blocking
///     id is Skala's own fault, and the default branch says so without having to list them.
/// </remarks>
static class IncompleteIds {
    /// <summary>The file could not be read or written.</summary>
    public const string FileIoFailed = "SK9015";

    /// <summary>The file does not parse.</summary>
    public const string NotParseable = "SK9010";
}

/// <summary>
///     Every human- and machine-facing surface, rendered from the one <see cref="RunReport" />.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/09: <b>no renderer contains analysis logic.</b> A renderer that decides what counts
///     as a failure is a second implementation of the gate, and the two will disagree on the day it
///     matters. Renderers read; the gate decides, once, into <see cref="RunReport.Gate" />. The only
///     arithmetic here is counting and sorting.
/// </remarks>
public static class Renderer {
    public static string Render(RunReport report, ReportFormat format, bool includeHints = false) =>
        format switch {
            ReportFormat.Plain => Plain(report, includeHints),
            ReportFormat.Json => SarifWriter.Serialize(SarifWriter.Build(report)),
            ReportFormat.Github => Github(report, includeHints),
            ReportFormat.Agent => AgentRenderer.Render(report),
            ReportFormat.Markdown => MarkdownRenderer.Render(report, includeHints),
            ReportFormat.JUnit => JUnitRenderer.Render(report, includeHints),
            _ => Terminal(report, includeHints)
        };

    /// <summary>
    ///     <c>--summary</c>: doc 09's last three lines and nothing else.
    /// </summary>
    /// <remarks>
    ///     ⚠ Rendered from the same report by the same code as the tail of <see cref="Terminal" />, not
    ///     re-derived. Two implementations of "the summary" is two chances for the summary to disagree
    ///     with the report it summarises.
    /// </remarks>
    public static string Summary(RunReport report) {
        var builder = new StringBuilder();
        Tail(builder, report);
        return builder.ToString();
    }

    /// <summary>The totals, the metrics and the gate — the three lines doc 09's example ends with.</summary>
    static void Tail(StringBuilder builder, RunReport report) {
        builder.Append("  ").Line(ReportTotals.Render(report));

        var thresholds = report.Gate is null ? null : GateThresholds(report);
        var metrics = report.Metrics.Render(thresholds);
        if (metrics.Length > 0) {
            builder.Append("  ").Line(metrics);
        }

        if (report.Gate is { } gate) {
            builder.Append("  gate `")
                .Append(gate.Name)
                .Append("`: ")
                .Append(gate.Passed ? "PASS" : "FAIL")
                .Append(" in ")
                .Append(FormatDuration(report.Duration))
                .Line(report.Partial ? "  ·  ⚠ partial run" : string.Empty);

            foreach (var failure in gate.Failures) {
                builder.Append("    ").Line(failure);
            }

            return;
        }

        builder.Append("  ")
            .Append(FormatDuration(report.Duration))
            .Line(report.Partial ? "  ·  ⚠ partial run" : string.Empty);
    }

    /// <summary>
    ///     ⚠ The thresholds are read back off the report's own gate result rather than re-read from
    ///     <c>skala.jsonc</c>. A renderer that re-read the configuration could render a gate line that
    ///     disagrees with the verdict beside it.
    /// </summary>
    static System.Collections.Immutable.ImmutableDictionary<string, double>? GateThresholds(RunReport report) =>
        report.GateThresholds.IsEmpty ? null : report.GateThresholds;

    /// <summary>
    ///     ⚠ Determinism is enforced after the fact, not during (docs/plan/07 § "Parallelism").
    ///     Analyzers run concurrently; the order they finish in may never be observable in output, so
    ///     every renderer sorts through here.
    /// </summary>
    public static IEnumerable<Finding> Ordered(RunReport report, bool includeHints) =>
        report.Reportable
            .Where(finding => includeHints || finding.Severity > SkalaSeverity.Hidden)
            .OrderBy(finding => SarifWriter.Relative(report.RepositoryRoot, finding.Path), StringComparer.Ordinal)
            .ThenBy(static finding => finding.Line)
            .ThenBy(static finding => finding.Column)
            .ThenBy(static finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Message, StringComparer.Ordinal);

    /// <summary>
    ///     The run's own error-severity diagnostics — the ones that mean
    ///     <b>
    ///         this run did not cover
    ///         what it was asked to cover
    ///     </b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ #345. A diagnostic is not a finding: a finding is something in the code, and one of these
    ///     is Skala failing. They are what <c>ExitCodes.InternalError</c> is made of — <c>SK9098</c> and
    ///     <c>SK9096</c> (an arrangement the safety layer reverted), <c>SK9099</c> (the formatter's
    ///     output was not token-equivalent) and <c>SK9015</c> (a file could not be read) — and until
    ///     #345 only <see cref="Terminal" /> and <see cref="Github" /> printed them at all. <c>plain</c>
    ///     rendered <b>zero bytes</b> and <c>agent</c> rendered <c>OK  nothing to do.</c>, which is the
    ///     exact string <c>verify</c>'s contract reserves for exit 0, on a run that exited 5.
    ///     <para>
    ///         ⚠ This is not a second gate (docs/plan/09 forbids that). It decides nothing: the exit code
    ///         was already decided by <c>CheckCommand</c> from these same diagnostics, and this only
    ///         reads them. What it fixes is that a renderer was silently dropping the half of the report
    ///         that says the other half is incomplete.
    ///     </para>
    /// </remarks>
    public static IEnumerable<SkalaDiagnostic> Blocking(RunReport report) =>
        report.Diagnostics.Where(static diagnostic => diagnostic.Severity >= SkalaSeverity.Error);

    /// <summary>
    ///     Whether a diagnostic is about one file rather than about the stage that ran over them.
    /// </summary>
    /// <remarks>
    ///     ⚠ The repository root is excluded, and #355 turned that from a counting detail into a
    ///     correctness one. <c>ArrangementFindings</c> emits its stage summary under <c>SK9015</c>
    ///     <b>whatever</b> took the stage down — a token-stream failure, a reverted arrangement, an
    ///     unreadable file — and locates it at the root. Reading ids off that summary to decide what
    ///     the banner says would report every arrangement bug in the tree as a permissions problem.
    ///     The per-file diagnostics above it are the ones that know why.
    /// </remarks>
    internal static bool IsFileScoped(RunReport report, SkalaDiagnostic diagnostic) =>
        diagnostic.File is { Length: > 0 } file
        && !string.Equals(
            file.TrimEnd(Path.DirectorySeparatorChar),
            report.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.Ordinal
        );

    /// <summary>
    ///     The files a <see cref="Blocking" /> diagnostic took out of the run.
    /// </summary>
    /// <remarks>
    ///     ⚠ The repository root is excluded: the summary diagnostic <c>ArrangementFindings</c> adds
    ///     after a per-file failure carries the root as its location, and counting it would report one
    ///     more unchecked file than there are.
    ///     <para>
    ///         ⚠ #360: so is a gate input. <c>SK9028</c> is located at the baseline it could not read,
    ///         which is away from the root and is not a source file, and counting it made a one-file
    ///         tree with a conflicted baseline read <c>1 of 1 file was not checked</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ #361: and so is a project. <c>SK9024</c>/<c>SK9029</c> at error severity sit at the
    ///         <c>.csproj</c> the workspace rung could not load, and the binlog ladder carries them
    ///         into the loose report. With both kinds out,
    ///         <b>
    ///             every path this yields is one the loader
    ///             put into <see cref="RunReport.FileCount" />
    ///         </b> — the per-file ids are located at a
    ///         reportable or unreadable source path by construction — which is what lets
    ///         <c>Scale</c> print the fraction without a guard.
    ///     </para>
    /// </remarks>
    public static IEnumerable<string> BlockedFiles(RunReport report) =>
        BlockingFileDiagnostics(report)
            .Select(static diagnostic => diagnostic.File!)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    ///     The blocking diagnostics that are about one source file: located away from the root, and
    ///     carrying a cause that <see cref="IsAboutAFile" />.
    /// </summary>
    static IEnumerable<SkalaDiagnostic> BlockingFileDiagnostics(RunReport report) =>
        Blocking(report).Where(diagnostic => IsFileScoped(report, diagnostic) && IsAboutAFile(CauseOf(diagnostic)));

    /// <summary>
    ///     Whether a cause takes a <em>source file</em> out of the run, as opposed to describing
    ///     something the run needed and did not have.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the one place the split is made. A cause that is not about a file is never in
    ///     <see cref="BlockedFiles" />, never in <see cref="Causes" /> and never in the fraction; the
    ///     banner reads it from <see cref="OutsideTheFraction" /> and gives it its own sentence. Adding
    ///     a member to <see cref="IncompleteCause" /> means deciding which side it is on, here.
    /// </remarks>
    public static bool IsAboutAFile(IncompleteCause cause) =>
        cause is not (IncompleteCause.GateInput or IncompleteCause.LoadRung);

    /// <summary>
    ///     The blocking diagnostics that are not about a source file: an input the gate compares
    ///     against that could not be read, or a project the load found and could not load.
    /// </summary>
    /// <remarks>
    ///     ⚠ Error severity only, because <see cref="Blocking" /> already is. The same ids at warning
    ///     are states a repository passes through — a baseline the gate names and that does not exist
    ///     yet (#358), MSBuild's relayed <c>workspace:</c> lines, a tree with no project at all — all
    ///     deliberately non-blocking, and none reaches the banner.
    ///     <para>
    ///         Not filtered by location: the baseline variant sits at the baseline's path and the
    ///         <c>--since</c> and <c>--no-new-suppressions</c> variants at the root; a project that
    ///         would not load sits at the <c>.csproj</c> and an ambiguity between several at the root.
    ///         All of them fail the reliability gate at exit 1, and before #360 and #361 the
    ///         root-located ones printed <c>this run did not finish — this is a Skala bug</c> above it.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<SkalaDiagnostic> OutsideTheFraction(RunReport report) => [
        .. Blocking(report).Where(static diagnostic => !IsAboutAFile(CauseOf(diagnostic)))
    ];

    /// <summary>The subset of <see cref="OutsideTheFraction" /> that is <see cref="IncompleteCause.GateInput" />.</summary>
    public static IReadOnlyList<SkalaDiagnostic> GateInputs(RunReport report) => [
        .. OutsideTheFraction(report).Where(static diagnostic => CauseOf(diagnostic) == IncompleteCause.GateInput)
    ];

    /// <summary>
    ///     Why a file dropped out of the run, from the diagnostic that dropped it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The default is <see cref="IncompleteCause.Defect" />, deliberately.</b> Only the five
    ///     ids that are positively known to describe the environment or the repository are named;
    ///     anything else the tool can fail with is Skala's own until somebody says otherwise. Getting
    ///     that backwards would let a new blocking id ship quietly telling readers to go and check
    ///     their file permissions.
    /// </remarks>
    public static IncompleteCause CauseOf(SkalaDiagnostic diagnostic) =>
        diagnostic.Id switch {
            IncompleteIds.FileIoFailed => IncompleteCause.Unreadable,
            IncompleteIds.NotParseable => IncompleteCause.Unparseable,
            ConfigDiagnosticIds.GateInputUnavailable => IncompleteCause.GateInput,
            ConfigDiagnosticIds.NothingToLoad
                or ConfigDiagnosticIds.AnalyzerAssemblyMissing => IncompleteCause.LoadRung,
            _ => IncompleteCause.Defect
        };

    /// <summary>
    ///     Every cause that took a file out of this run, strongest first, with the files it took.
    /// </summary>
    /// <remarks>
    ///     ⚠ #355 decided the mixed run this way: <b>name every cause, each with its own count</b>. A
    ///     banner that reports only the first cause is the same defect the issue is about, one level
    ///     down — a reader sent to <c>chmod</c> over a tree that also holds a token-stream failure is
    ///     as misdirected as one sent to file a bug over a mode-600 file. Skala's own fault is stated
    ///     first because it is the one the reader cannot fix and the one that has to be reported.
    ///     <para>
    ///         ⚠ A file carrying two blocking diagnostics is attributed once, to its strongest cause, so
    ///         the per-cause counts sum to the fraction the banner opens with. Two numbers in one
    ///         sentence that do not add up is a sentence nobody trusts twice.
    ///     </para>
    ///     <para>
    ///         ⚠ A run whose only blocking diagnostics are stage summaries at the root has no per-file
    ///         cause to read and comes back as <see cref="IncompleteCause.Defect" /> with no files —
    ///         which is the pre-#355 sentence, unchanged, for the case where nothing is known.
    ///     </para>
    ///     <para>
    ///         ⚠ #360, #361: a cause that is not <see cref="IsAboutAFile" /> never appears here. It is
    ///         not a cause a file dropped out for, so it has no count to add to the fraction; the
    ///         banner reads it from <see cref="OutsideTheFraction" /> and states it as its own sentence.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<(IncompleteCause Cause, int Files)> Causes(RunReport report) {
        var strongest = new Dictionary<string, IncompleteCause>(StringComparer.Ordinal);
        foreach (var diagnostic in BlockingFileDiagnostics(report)) {
            var cause = CauseOf(diagnostic);
            if (!strongest.TryGetValue(diagnostic.File!, out var known) || cause < known) {
                strongest[diagnostic.File!] = cause;
            }
        }

        if (strongest.Count == 0) {
            return [(IncompleteCause.Defect, 0)];
        }

        return [
            .. strongest.Values
                .GroupBy(static cause => cause)
                .OrderBy(static group => group.Key)
                .Select(static group => (group.Key, group.Count()))
        ];
    }

    /// <summary>
    ///     A diagnostic's detail, flattened to one line.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="SkalaDiagnostic.Detail" /> is where the crash-reproduction path lives — "A
    ///     reproduction is in …/.skala/crash/&lt;hash&gt;. This is a Skala bug; the file was left
    ///     untouched." — and before #345 <b>no surface printed it</b>, the SARIF included. Flattened
    ///     because <c>plain</c> is one line per diagnostic by contract and <c>agent</c> is budgeted;
    ///     the one multi-line detail in the tree is the arrange summary's captured sub-output, whose
    ///     per-file lines are already rendered above it as their own diagnostics.
    /// </remarks>
    internal static string? OneLine(string? detail) {
        if (detail is not { Length: > 0 }) {
            return null;
        }

        var line = detail.AsSpan();
        var end = line.IndexOfAny('\r', '\n');
        if (end >= 0) {
            line = line[..end];
        }

        var text = line.Trim().ToString();
        return text.Length > 0 ? text : null;
    }

    /// <summary>Where a tool diagnostic happened, as every surface displays a path.</summary>
    internal static string Relative(RunReport report, SkalaDiagnostic diagnostic) =>
        diagnostic.File is { Length: > 0 } file ? SarifWriter.Relative(report.RepositoryRoot, file) : ".";

    /// <summary>
    ///     The detail <c>plain</c> and <c>agent</c> print — the two bounded surfaces.
    /// </summary>
    /// <remarks>
    ///     ⚠ File-scoped diagnostics only. A diagnostic located at the repository root is a summary of
    ///     the ones above it, and its detail is the sub-command's transcript: for an arrangement revert
    ///     that transcript restates, verbatim, the <c>SK9098</c> already printed as its own line. One
    ///     copy of a 300-character message is the report; two is the budget going on noise.
    ///     <see cref="Terminal" /> is unbounded and prints both.
    /// </remarks>
    internal static string? BoundedDetail(RunReport report, SkalaDiagnostic diagnostic) =>
        Relative(report, diagnostic) == "." ? null : OneLine(diagnostic.Detail);

    /// <summary>
    ///     The message a bounded surface prints for a tool diagnostic, detail folded in.
    /// </summary>
    internal static string Sentence(RunReport report, SkalaDiagnostic diagnostic) =>
        BoundedDetail(report, diagnostic) is { } detail
            ? diagnostic.Message + " — " + detail
            : diagnostic.Message;

    /// <summary>
    ///     ⚠ #345, first and in plain's own <c>path:line:col: level id: message</c> shape, so that the
    ///     editor error parser this format exists for lands the reader on the file that was not
    ///     checked. Before this the whole method emitted <b>zero bytes</b> on an exit-5 run.
    /// </summary>
    static void PlainBlocking(StringBuilder builder, RunReport report) {
        foreach (var diagnostic in Blocking(report)) {
            builder.Append(Relative(report, diagnostic))
                .Append(':')
                .Append(Math.Max(1, diagnostic.Line).ToString(CultureInfo.InvariantCulture))
                .Append(":1: ")
                .Append(Word(diagnostic.Severity))
                .Append(' ')
                .Append(diagnostic.Id)
                .Append(": ")
                .Line(Sentence(report, diagnostic));
        }
    }

    static string Plain(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        PlainBlocking(builder, report);

        foreach (var finding in Ordered(report, includeHints)) {
            builder.Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(finding.Column.ToString(CultureInfo.InvariantCulture))
                .Append(": ")
                .Append(Word(finding.Severity))
                .Append(' ')
                .Append(finding.RuleId)
                .Append(": ")
                .Line(finding.Message);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     ⚠ #345: this loop printed <see cref="SkalaDiagnostic.ToString" /> and dropped
    ///     <see cref="SkalaDiagnostic.Detail" /> — which is where the crash-reproduction path lives.
    ///     <c>.skala/crash/</c> on disk was the only place that path appeared, on any surface, and
    ///     noticing the directory is how the bug was found rather than from any output.
    /// </summary>
    /// <remarks>
    ///     ⚠ Unbounded, unlike <see cref="BoundedDetail" />'s callers: this is the human default and
    ///     has no context window to protect, so the arrange summary's transcript is printed too.
    /// </remarks>
    static void TerminalDiagnostics(StringBuilder builder, RunReport report) {
        foreach (var diagnostic in report.Diagnostics.Where(static d => d.Severity >= SkalaSeverity.Info)) {
            builder.Append("  ").Line(diagnostic.ToString());

            if (OneLine(diagnostic.Detail) is { } detail) {
                builder.Append("    ").Line(detail);
            }
        }
    }

    static string Terminal(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        builder.Append(Path.GetFileName(report.RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar)))
            .Append("  ·  ")
            .Append(report.FileCount.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" files  ·  ")
            .Append(report.LineCount.ToString("N0", CultureInfo.InvariantCulture))
            .Append(" lines  ·  ")
            .Line(report.LoadSummary);
        builder.Line();

        var findings = Ordered(report, includeHints).ToList();
        foreach (var group in findings.GroupBy(
                     finding => SarifWriter.Relative(report.RepositoryRoot, finding.Path),
                     StringComparer.Ordinal
                 )) {
            builder.Append("  ").Line(group.Key);
            foreach (var finding in group) {
                builder.Append("    ")
                    .Append(finding.HasFix ? "⟳ " : "  ")
                    .Append(
                        (finding.Line.ToString(CultureInfo.InvariantCulture)
                            + ":"
                            + finding.Column.ToString(CultureInfo.InvariantCulture)).PadRight(9)
                    )
                    .Append(Word(finding.Severity).PadRight(11))
                    .Append(finding.RuleId)
                    .Append("  ")
                    .Line(finding.Message);
            }

            builder.Line();
        }

        TerminalDiagnostics(builder, report);

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("  ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run: ")
                .Line(string.Join(", ", report.SkippedRules.Select(static rule => rule.RuleId)));

            // ⚠ Without --verbose this prints the first rule's reason and lets it stand for all of
            // them, which is right when they share one — "no compilation" skips every semantic rule
            // for the same reason — and wrong the moment they do not.
            if (report.Verbose) {
                foreach (var rule in report.SkippedRules) {
                    builder.Append("    ").Append(rule.RuleId).Append("  ").Line(rule.Reason);
                }
            } else {
                builder.Append("  ").Line(report.SkippedRules[0].Reason);
            }

            builder.Line();
        }

        Tail(builder, report);
        return builder.ToString();
    }

    /// <summary>
    ///     ⚠ The annotations, and then the verdict — because a log that ends at the annotations does not
    ///     say what happened.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This renderer used to emit findings and nothing else. The gate's verdict and its reasons go
    ///         to <c>$GITHUB_STEP_SUMMARY</c>, which is a different page, so the *log* of a failing
    ///         `Check` step was two hundred annotations followed by
    ///         <c>
    /// Process completed with exit code
    ///          1
    ///         </c> and no statement of why. Read from the log alone, this repository's own master gate
    ///         looked like twenty-four errors in one rule family; it was in fact failing four conditions,
    ///         of which those errors were one, and the largest was that the baseline the `ci` gate names
    ///         did not exist.
    ///     </para>
    ///     <para>
    ///         ⚠ That last one is the reason the notifications are emitted too, and not only the gate
    ///         failures. <c>SK9030</c> says in as many words: "the gate names a baseline at
    ///         .skala/baseline.sarif and there is no such file, so every finding counts as new." The tool
    ///         had diagnosed itself correctly and put the answer somewhere the log could not reach.
    ///     </para>
    ///     <para>
    ///         ⚠ It is not a second gate (doc 09 forbids that, and this file's own remarks repeat it). It
    ///         reads <see cref="RunReport.Gate" /> — the verdict the gate already reached — and prints it.
    ///         Nothing here decides anything.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The run's own diagnostics about the run: a missing baseline, a binlog that covers too
    ///     little, a rule that could not be loaded. They are what explains the numbers above them.
    /// </summary>
    /// <remarks>
    ///     ⚠ #345: these carried no <c>file=</c>, so a diagnostic about a specific file annotated the
    ///     workflow rather than the code — and an <c>SK9098</c> naming no file is a reviewer being told
    ///     that something was not checked and not which thing. The detail is appended for the same
    ///     reason it is everywhere else: it is where the crash-reproduction path is.
    /// </remarks>
    static void GithubDiagnostics(StringBuilder builder, RunReport report) {
        foreach (var diagnostic in report.Diagnostics) {
            builder.Append(diagnostic.Severity >= SkalaSeverity.Error ? "::error" : "::notice");

            if (diagnostic.File is { Length: > 0 }) {
                builder.Append(" file=")
                    .Append(Relative(report, diagnostic))
                    .Append(",line=")
                    .Append(Math.Max(1, diagnostic.Line).ToString(CultureInfo.InvariantCulture));
            }

            builder.Append("::")
                .Append(diagnostic.Id)
                .Append(": ")
                .Line(Sentence(report, diagnostic).Replace("\n", "%0A", StringComparison.Ordinal));
        }
    }

    static string Github(RunReport report, bool includeHints) {
        var builder = new StringBuilder();
        foreach (var finding in Ordered(report, includeHints)) {
            builder.Append("::")
                .Append(
                    finding.Severity switch {
                        SkalaSeverity.Error => "error",
                        SkalaSeverity.Warning => "warning",
                        _ => "notice"
                    }
                )
                .Append(" file=")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(",line=")
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append(",col=")
                .Append(finding.Column.ToString(CultureInfo.InvariantCulture))
                .Append(",title=")
                .Append(finding.RuleId)
                .Append("::")
                .Line(finding.Message.Replace("\n", "%0A", StringComparison.Ordinal));
        }

        GithubDiagnostics(builder, report);

        if (report.Gate is { } gate) {
            builder.Append(gate.Passed ? "::notice::" : "::error::")
                .Append("gate `")
                .Append(gate.Name)
                .Append("`: ")
                .Line(gate.Passed ? "PASS" : "FAIL");

            foreach (var failure in gate.Failures) {
                builder.Append("::error::  ").Line(failure.Replace("\n", "%0A", StringComparison.Ordinal));
            }
        }

        return builder.ToString();
    }

    internal static string Word(SkalaSeverity severity) =>
        severity switch {
            SkalaSeverity.Error => "error",
            SkalaSeverity.Warning => "warning",
            SkalaSeverity.Info => "suggestion",
            _ => "hint"
        };

    internal static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds < 1
        ? duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms"
        : duration.TotalSeconds < 90
            ? duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s"
            : ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture)
            + " m "
            + duration.Seconds.ToString(CultureInfo.InvariantCulture)
            + " s";
}

/// <summary>
///     The three-bucket report of docs/plan/10 § "Agent-shaped output".
/// </summary>
/// <remarks>
///     Every line of this is a decision from that document, and the ordering is the load-bearing one:
///     <list type="number">
///         <item>
///             <b>FORMAT first</b>, because it is free and unconditional. An agent reading top-down does the
///             cheap work first and arrives at the hard part with a clean tree.
///         </item>
///         <item><b>FIXABLE second</b>, because the next command is mechanical.</item>
///         <item><b>ACTION last</b>, because it is the only part that needs the model to think.</item>
///     </list>
///     ⚠ The command to run is printed complete, with paths. Not "run skala format" — the exact
///     invocation, which removes a whole class of agent error (guessing flags) at the cost of a longer
///     line.
///     <para>
///         ⚠ Output is bounded. An unbounded lint dump eats the context window the agent needs in order to
///         fix anything, so the cap is real and the elision says exactly what was elided and how to see it.
///     </para>
/// </remarks>
public static class AgentRenderer {
    public const int MaxFindings = 50;
    public const int MaxCharacters = 8000;

    /// <summary>
    ///     Where a reader of a truncated agent report is sent for the rest of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ #345: this said <c>skala check --format=json</c>, and
    ///     <b>
    ///         `skala check` does not run the
    ///         arrangement stage
    ///     </b> — <c>VerifyCommand</c> is the only caller that sets
    ///     <c>IncludeArrangement</c>. So the advice printed when the agent report elided something was
    ///     guaranteed not to surface an arrangement message, which is precisely the message #345 is
    ///     about. `verify` is a superset of `check` here, so this is the right pointer from either.
    /// </remarks>
    /// <remarks>
    ///     ⚠ <c>internal</c>, unlike <see cref="MaxFindings" /> and <see cref="MaxCharacters" /> beside
    ///     it: SK6034 — a <c>public const</c> is copied into every caller at compile time, so shipping
    ///     a new value leaves every caller that is not rebuilt on the old one, with no error anywhere.
    ///     Nothing outside this assembly needs the string.
    /// </remarks>
    internal const string FullReportCommand = "skala verify --format=json";

    public static string Render(RunReport report) {
        var builder = new StringBuilder();

        // ⚠ <b>Scoped, because this report is a queue and a queue nobody can drain is noise.</b>
        // Every one of the three buckets below is phrased as work to do — "needs a decision", "run
        // skala fix", "run skala format" — so a finding the repository has already accepted, or one
        // on a line this branch never touched, does not belong in any of them. On the first
        // repository to adopt Skala this was the difference between 778 findings needing a decision
        // on every run for ever and 3.
        //
        // ⚠ With neither `--baseline` nor `--since` in play `IsNew` is true for everything, so the
        // unscoped output is byte-for-byte what it was.
        var ordered = Renderer.Ordered(report, false).Where(report.IsNew).ToList();

        Incomplete(builder, report);

        var formatting = ordered.Where(static f => f.RuleId == RuleIds.FileIsNotFormatted).ToList();
        var fixable = ordered
            .Where(static f => f.RuleId != RuleIds.FileIsNotFormatted && f.HasFix && f.FixIsSafe)
            .ToList();
        var action = ordered
            .Where(static f => f.RuleId != RuleIds.FileIsNotFormatted && (!f.HasFix || !f.FixIsSafe))
            .ToList();

        if (formatting.Count > 0) {
            var paths = formatting
                .Select(finding => Quote(SarifWriter.Relative(report.RepositoryRoot, finding.Path)))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            builder.Append("FORMAT  ")
                .Append(paths.Count.ToString(CultureInfo.InvariantCulture))
                .Append(paths.Count == 1 ? " file needs" : " files need")
                .Append(" formatting — run: skala format ")
                .Line(string.Join(" ", paths.Take(20)));
            if (paths.Count > 20) {
                builder.Append("        … and ")
                    .Append((paths.Count - 20).ToString(CultureInfo.InvariantCulture))
                    .Line(" more; `skala format .` does all of them.");
            }

            builder.Line();
        }

        var budget = MaxFindings;
        if (fixable.Count > 0) {
            builder.Append("FIXABLE ")
                .Append(fixable.Count.ToString(CultureInfo.InvariantCulture))
                .Append(fixable.Count == 1 ? " finding has" : " findings have")
                .Line(" safe automatic fixes — run: skala fix --safe");
            budget -= Emit(builder, report, fixable, budget, "  ");
            builder.Line();
        }

        if (action.Count > 0) {
            builder.Append("ACTION  ")
                .Append(action.Count.ToString(CultureInfo.InvariantCulture))
                .Append(action.Count == 1 ? " finding needs" : " findings need")
                .Line(" a decision");
            Emit(builder, report, action, budget, "  ");
            builder.Line();
        }

        var suppressed = report.Findings.Count(static f => f.Suppression is SuppressionKind.Pragma
                or SuppressionKind.Attribute
        );
        if (suppressed > 0) {
            // ⚠ docs/plan/10 point 3: given a warning and the ability to edit, `#pragma warning
            // disable` is a valid move for a model optimising for the check passing. Surfacing
            // suppressions unprompted is what makes the dishonest path visible.
            builder.Append(suppressed.ToString(CultureInfo.InvariantCulture))
                .Line(" finding(s) suppressed by #pragma or [SuppressMessage] — see: skala check --show-suppressions");
            builder.Line();
        }

        // ⚠ Said first and unconditionally when there is nothing to do. The SKIPPED block below is
        // context, not work, and an agent that reads a report starting with SKIPPED has to infer
        // that the answer was yes — inference the contract exists to remove.
        //
        // ⚠ #345: `Incomplete` writes into this same builder *before* every bucket, so a run that
        // could not finish can never reach here with an empty builder. That is the whole mechanism —
        // the string below is reserved for exit 0, and it was being printed on exit 5.
        if (builder.Length == 0) {
            builder.Line("OK  nothing to do.");
            if (!report.SkippedRules.IsEmpty) {
                builder.Line();
            }
        }

        if (!report.SkippedRules.IsEmpty) {
            builder.Append("SKIPPED ")
                .Append(report.SkippedRules.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" rule(s) did not run (")
                .Append(report.Mode.ToString().ToLowerInvariant())
                .Line(" load): " + string.Join(", ", report.SkippedRules.Select(static r => r.RuleId)));
            builder.Line();
        }

        var text = builder.ToString();
        if (text.Length > MaxCharacters) {
            text = text[..MaxCharacters]
                + "\n… output truncated at "
                + MaxCharacters.ToString(CultureInfo.InvariantCulture)
                + " characters. Run `"
                + FullReportCommand
                + "` for all of it.\n";
        }

        return text;
    }

    /// <summary>
    ///     ⚠ #345. The block that goes <b>above FORMAT</b>, because this format exists to be read by
    ///     something that will otherwise act on <c>OK</c>.
    /// </summary>
    /// <remarks>
    ///     The ordering argument in this class's own remarks is "cheap work first". It is outranked by
    ///     one thing only: a statement that the report underneath does not cover the whole tree. An
    ///     agent that formats three files and reports success, on a run where Skala could not check a
    ///     fourth, has been told something false — and the cost measured in #345 was a ~750-file
    ///     repository where <c>verify</c> exited 5 for weeks while printing <c>OK  nothing to do.</c>,
    ///     found by noticing <c>.skala/crash/</c> on disk rather than from any output.
    ///     <para>
    ///         ⚠ The count is stated as a fraction. "1 file could not be checked" invites the reading
    ///         that one file is the whole problem; "1 of 754" says the other 753 were covered, which is
    ///         the partial verdict the issue asked for and the reason exit 5 is worth reading at all.
    ///     </para>
    ///     <para>
    ///         ⚠ #355: the cause is stated per run, not assumed. This line said "this is a Skala bug"
    ///         over an unreadable file, one line above a per-file <c>SK9015</c> that
    ///         <c>ExitCodeContractTests</c> asserts is <em>not</em> reported as one — the two had
    ///         drifted because no test read them together. <see cref="Renderer.Causes" /> holds the
    ///         mixed-run decision.
    ///     </para>
    ///     <para>
    ///         ⚠ #360: a gate input that could not be read is a <b>separate sentence</b>, outside the
    ///         fraction. The fraction is <c>N of M files</c> and a baseline is not one of the M, so
    ///         folding it in made a one-file tree with a conflicted baseline read "1 of 1 file was
    ///         not checked — this is a Skala bug" when the file was checked and the bug was a merge.
    ///         When nothing else blocked, the line opens with the baseline and says outright that
    ///         every file was checked, because on this surface — which prints no gate verdict — the
    ///         banner is the only thing that explains the exit code.
    ///     </para>
    ///     <para>
    ///         ⚠ #361: a project the load found and could not open is the same shape one id over.
    ///         The binlog ladder falls through a failed workspace rung to loose and keeps the rung's
    ///         error-severity <c>SK9024</c>/<c>SK9029</c>, located at the <c>.csproj</c>, so the same
    ///         one-file tree read "1 of 1 file was not checked — this is a Skala bug" above
    ///         <b>exit 0</b>. It is outside the fraction like the baseline, and its sentence says
    ///         where the rules went, because the gate now fails on it and this line is what explains
    ///         that exit.
    ///     </para>
    /// </remarks>
    static void Incomplete(StringBuilder builder, RunReport report) {
        var blocking = Renderer.Blocking(report).ToList();
        if (blocking.Count == 0) {
            return;
        }

        var outside = Renderer.OutsideTheFraction(report);

        // ⚠ "The run was blocked" is anything blocking that is about a file — a per-file diagnostic,
        // or the arrange stage's root-located summary. A gate input or a failed load rung alone
        // leaves every file checked, and the sentence has to say which.
        var runBlocked = blocking.Count > outside.Count;
        builder.Append("INCOMPLETE  ");

        if (runBlocked) {
            builder.Append(Scale(report)).Append(" — ").Append(Because(Renderer.Causes(report)));
        }

        if (outside.Count > 0) {
            var clause = OutsideTheFractionClause(report, outside);
            if (runBlocked) {
                builder.Append(' ').Append(char.ToUpperInvariant(clause[0])).Append(clause.AsSpan(1));
            } else {
                builder.Append(clause);
            }
        }

        builder.Line(Trailer(runBlocked, outside));

        foreach (var diagnostic in blocking) {
            builder.Append("  ")
                .Append(diagnostic.Id)
                .Append("  ")
                .Append(Renderer.Relative(report, diagnostic))
                .Append("  ")
                .Line(diagnostic.Message);

            // ⚠ The detail carries the crash-reproduction path. Dropping it is how #345 stayed
            // undiagnosed: the artefact existed on disk and nothing said where.
            if (Renderer.BoundedDetail(report, diagnostic) is { } detail) {
                builder.Append("        → ").Line(detail);
            }
        }

        builder.Line();
    }

    /// <summary>
    ///     The clause after the fraction: whose fault it was, and what to do about it.
    /// </summary>
    /// <remarks>
    ///     One cause is a sentence; more than one is each cause with its own count, Skala's first.
    ///     Kept to a line either way — the banner is read by something deciding whether to trust the
    ///     verdict under it, and a paragraph there is a paragraph it will skim.
    /// </remarks>
    static string Because(IReadOnlyList<(IncompleteCause Cause, int Files)> causes) {
        // ⚠ Stated positively. "Not a Skala bug" was the first draft and contains the two words a
        // hook greps for and a model keys on; a negation is a weaker signal than naming the cause.
        if (causes.Count == 1) {
            return causes[0].Cause switch {
                IncompleteCause.Unreadable =>
                    "could not be read: check permissions and that the path is still mounted.",
                IncompleteCause.Unparseable =>
                    "unparseable: fix the syntax error; the file was left byte-identical (ADR-003).",
                _ => "this is a Skala bug, not a finding in your code."
            };
        }

        var clauses = causes.Select(static entry =>
            entry.Files.ToString(CultureInfo.InvariantCulture)
            + entry.Cause switch {
                IncompleteCause.Unreadable =>
                    " could not be read (check permissions and that the path is still mounted)",
                IncompleteCause.Unparseable => " unparseable (left byte-identical, ADR-003)",
                _ => " a Skala bug, not a finding in your code"
            }
        );
        return string.Join("; ", clauses) + ".";
    }

    /// <summary>
    ///     The sentence(s) for what the run needed and did not have: an input the gate could not
    ///     read, a project the load could not open — what it was, where, and what that did to the
    ///     verdict. Lower-case at the start so the caller can open the line with it or capitalise it
    ///     after the fraction.
    /// </summary>
    /// <remarks>
    ///     A baseline or a project is named by its relative path — it is the thing to open and fix,
    ///     and the file the reader would otherwise go looking for among the source files. The
    ///     root-located variants (<c>--since</c>, <c>--no-new-suppressions</c>, several
    ///     <c>.csproj</c> and no <c>--project</c>) have no path worth printing and the diagnostic line
    ///     under the banner carries their detail, so they are one generic clause however many there
    ///     are.
    ///     <para>
    ///         ⚠ #361: the load-rung sentence says where the rules went. The <c>agent</c> surface
    ///         prints no gate verdict, so this line and the <c>SKIPPED</c> line are the only things on
    ///         it that explain why a report full of findings exits 1.
    ///     </para>
    /// </remarks>
    static string OutsideTheFractionClause(RunReport report, IReadOnlyList<SkalaDiagnostic> outside) {
        var sentences = new List<string>();

        var gateInputs = outside.Where(static d => Renderer.CauseOf(d) == IncompleteCause.GateInput).ToList();
        if (gateInputs.Count > 0) {
            var clauses = gateInputs
                .Where(diagnostic => Renderer.IsFileScoped(report, diagnostic))
                .Select(diagnostic => "the baseline at " + Renderer.Relative(report, diagnostic) + " could not be read")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (gateInputs.Any(diagnostic => !Renderer.IsFileScoped(report, diagnostic))) {
                clauses.Add("an input the gate scopes by could not be read (SK9028 below)");
            }

            sentences.Add(string.Join(" and ", clauses) + ", so the gate compared against nothing.");
        }

        var loadRungs = outside.Where(static d => Renderer.CauseOf(d) == IncompleteCause.LoadRung).ToList();
        if (loadRungs.Count > 0) {
            var clauses = loadRungs
                .Where(diagnostic => Renderer.IsFileScoped(report, diagnostic))
                .Select(diagnostic => Renderer.Relative(report, diagnostic) + " could not be loaded")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (loadRungs.Any(diagnostic => !Renderer.IsFileScoped(report, diagnostic))) {
                clauses.Add("no project could be loaded (" + loadRungs[0].Id + " below)");
            }

            sentences.Add(
                string.Join(" and ", clauses)
                + ", so the run fell back to "
                + report.Mode.ToString().ToLowerInvariant()
                + " and the rules that need a compilation did not run."
            );
        }

        // The caller capitalises the first sentence when it follows the fraction; any later one
        // opens a sentence of its own and is capitalised here.
        return string.Join(
            " ",
            sentences.Select(static (sentence, index) =>
                index == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..]
            )
        );
    }

    /// <summary>
    ///     What the report under the banner covers, given what blocked it. One sentence, assembled
    ///     from the causes in play rather than switched over their combinations.
    /// </summary>
    static string Trailer(bool runBlocked, IReadOnlyList<SkalaDiagnostic> outside) {
        var gateInput = outside.Any(static d => Renderer.CauseOf(d) == IncompleteCause.GateInput);
        var loadRung = outside.Any(static d => Renderer.CauseOf(d) == IncompleteCause.LoadRung);

        var trailer = new StringBuilder(runBlocked ? " Everything below covers the rest" : " Every file was checked");
        if (loadRung) {
            trailer.Append(runBlocked ? " with the rules that could run" : " by the rules that could run");
        }

        if (gateInput) {
            trailer.Append(
                runBlocked
                    ? ", shown as if there were nothing to compare against"
                    : "; everything below is shown as if there were nothing to compare against"
            );
        }

        if (loadRung) {
            trailer.Append("; the SKIPPED line names the rules that did not run");
        }

        return trailer.Append('.').ToString();
    }

    /// <summary>
    ///     How much of the tree the incomplete run missed, as a fraction of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ "1 file could not be checked" invites the reading that one file is the whole problem;
    ///     "1 of 754" says the other 753 were covered, which is the partial verdict #345 asked for and
    ///     the reason exit 5 is worth reading at all.
    ///     <para>
    ///         ⚠ #356: <c>FileCount &lt; blocked</c> used to be reached by an ordinary tree. The
    ///         loaders counted a file only after reading it, so an unreadable file was in the
    ///         numerator and never in the denominator — <c>1 of 1</c> over a two-file tree, and with
    ///         two unreadable files beside one readable one this branch quietly dropped the fraction
    ///         instead of printing the <c>2 of 1</c> that would have exposed the arithmetic. Every
    ///         requested source file is now in <see cref="RunReport.FileCount" /> whether or not it
    ///         opened, so for the per-file blocking ids the inequality cannot hold and the branch is
    ///         not a safety net for them any more.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>There is no <c>FileCount &lt; blocked</c> guard, and that is deliberate.</b> #356
    ///         kept one because <c>SK9028</c> at the baseline reached it; #360 made that a gate input
    ///         and re-pinned it on <c>SK9024</c> at a <c>.csproj</c>; #361 made that a load rung and
    ///         enumerated every error-severity tool id once more. Nothing is left: <c>SK9015</c>,
    ///         <c>SK9010</c>, <c>SK9096</c>–<c>SK9099</c> are located at a source path the loader put
    ///         into <see cref="RunReport.FileCount" /> (reportable or unreadable); <c>SK9023</c> and
    ///         the arrange summary sit at the root; <c>SK9028</c>, <c>SK9024</c>, <c>SK9029</c> are
    ///         not <see cref="Renderer.IsAboutAFile" />; <c>SK9020</c>/<c>SK9021</c> are refused at exit
    ///         4 before a renderer runs; the config ids never enter a <c>RunReport</c>. So
    ///         <c>blocked &lt;= FileCount</c> is an invariant of <see cref="Renderer.BlockedFiles" />,
    ///         and <c>IncompleteBannerTests</c> asserts it per id rather than guarding it here — a
    ///         guard would print a plausible sentence over a broken denominator, and <c>2 of 1</c> is
    ///         the kind of wrong that gets reported the day it appears.
    ///     </para>
    /// </remarks>
    static string Scale(RunReport report) {
        var blocked = Renderer.BlockedFiles(report).Count();
        if (blocked == 0) {
            return "this run did not finish";
        }

        var noun = blocked == 1 ? " file was not checked" : " files were not checked";
        return blocked.ToString(CultureInfo.InvariantCulture)
            + " of "
            + report.FileCount.ToString(CultureInfo.InvariantCulture)
            + noun;
    }

    static int Emit(StringBuilder builder, RunReport report, List<Finding> findings, int budget, string indent) {
        var shown = 0;
        foreach (var finding in findings) {
            if (shown >= budget) {
                builder.Append(indent)
                    .Append("… ")
                    .Append((findings.Count - shown).ToString(CultureInfo.InvariantCulture))
                    .Append(" more elided. Run `")
                    .Append(FullReportCommand)
                    .Line("` for all of them.");
                break;
            }

            builder.Append(indent)
                .Append(finding.RuleId)
                .Append("  ")
                .Append(SarifWriter.Relative(report.RepositoryRoot, finding.Path))
                .Append(':')
                .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                .Append("  ")
                .Line(finding.Message);

            // ⚠ "Every finding either carries a fix or carries a one-sentence instruction. Never
            // both, never neither." A finding with no fix gets the rule's summary as an imperative.
            if (!finding.HasFix && RuleCatalog.Find(finding.RuleId) is { } rule) {
                builder.Append(indent).Append("        → ").Line(rule.Summary);
            }

            shown++;
        }

        return shown;
    }

    static string Quote(string path) => path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}

/// <summary>
///     <c>StringBuilder.AppendLine</c>, with the line ending fixed at <c>\n</c>.
/// </summary>
/// <remarks>
///     ⚠ <c>AppendLine</c> appends <see cref="Environment.NewLine" />, which is CRLF on Windows, so
///     every renderer in this file emitted CRLF there and LF everywhere else. Only one assertion in
///     the tree compared a whole rendered string against a literal —
///     <c>ReportingTests.AgentRenderer_SaysNothingToDoWhenThereIsNothingToDo</c>, expecting
///     <c>"OK  nothing to do.\n"</c> — so one test failed on Windows and the other seven surfaces
///     changed shape unobserved.
///     <para>
///         ⚠ It is not a cosmetic difference, because these are not all human surfaces. <c>plain</c> is
///         "greppable, and the format every editor's error parser already understands" (doc 09) and
///         <c>agent</c> is doc 10's machine report. An output contract that varies by the platform the
///         tool happens to run on is not a contract. <see cref="GithubRenderer" /> and
///         <see cref="MarkdownRenderer" />, one file over, already append <c>'\n'</c> by hand for exactly
///         this reason, and <c>DocsSite</c> makes the same argument at length; this file was the one that
///         had not been told.
///     </para>
/// </remarks>
static class Lines {
    extension(StringBuilder builder) {
        internal StringBuilder Line() => builder.Append('\n');

        internal StringBuilder Line(string text) => builder.Append(text).Append('\n');
    }
}
