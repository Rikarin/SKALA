using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>What a rule's zero on one vendored tree is allowed to mean.</summary>
/// <remarks>
///     ⚠ The distinction this enum exists for is the whole of issue #277. <see cref="Silent" /> and
///     <see cref="Declined" /> are both "0 findings", and only one of them is evidence.
/// </remarks>
public enum CorpusVerdict {
    /// <summary>The rule fired. Every finding is one a person has to read.</summary>
    Fired,

    /// <summary>
    ///     The rule reported nothing, and the canary planted into the same compilation fired — so the
    ///     analyzer ran, bound what it needed to bind, and declined. This is the only zero that counts
    ///     towards docs/plan/08's third clause.
    /// </summary>
    Declined,

    /// <summary>
    ///     ⚠ The rule reported nothing and
    ///     <em>
    ///         its own positive fixture, compiled into the same
    ///         compilation, reported nothing either
    ///     </em>. The zero says nothing about the tree; it says the
    ///     instrument did not work here. Never report this as "clean".
    /// </summary>
    Silent,

    /// <summary>No canary was planted for this rule, so the zero is unclassified.</summary>
    Unplanted
}

/// <summary>
///     A shape dropped into a sweep's compilation, so that a zero can be read.
/// </summary>
/// <remarks>
///     ⚠ Planted from the rule's own committed positive fixture rather than hand-written here. A
///     hand-written canary is a second statement of what the rule is about, and the two drift: the
///     canary keeps firing on a shape the rule stopped caring about, or stops firing for a reason that
///     is about the canary. <see cref="RuleCorpus.Canary" /> reads
///     <c>Rules/Rikarin.Skala.Rules.Tests/fixtures/&lt;id&gt;/positive/</c>, which
///     <c>RuleFixtureTests</c> already asserts the rule fires on one file at a time.
/// </remarks>
public sealed record PlantedShape(string RuleId, string Name, string Source) {
    /// <summary>A synthetic path, inside the tree so that findings on it are reportable.</summary>
    public string PathIn(string treeRoot) => Path.Combine(treeRoot, "__planted__", RuleId + "." + Name + ".cs");
}

/// <summary>What one tree's sweep produced, with the error bars beside the counts.</summary>
public sealed record CorpusSweepResult {
    public required string Tree { get; init; }

    /// <summary>Source files compiled — the tree's own, excluding the oracle fixture twins.</summary>
    public required int Files { get; init; }

    public required bool ImplicitUsings { get; init; }

    /// <summary>⚠ The error bar. A finding count without this is the thing issue #277 is about.</summary>
    public required int CompilerErrors { get; init; }

    /// <summary>
    ///     ⚠ <c>SK9030</c>: an analyzer threw and was disabled for the rest of the run.
    /// </summary>
    /// <remarks>
    ///     A crashed analyzer produces exactly the same clean zero as a correct one, so this being
    ///     non-zero invalidates every zero in the same run rather than being a footnote to it.
    /// </remarks>
    public required ImmutableArray<string> AnalyzerFailures { get; init; }

    /// <summary>Skala findings on the tree's own files. Planted files are not in here.</summary>
    public required ImmutableArray<Finding> Findings { get; init; }

    /// <summary>Rule ids whose planted canary fired.</summary>
    public required ImmutableSortedSet<string> CanariesFired { get; init; }

    /// <summary>⚠ Rule ids whose planted canary did <em>not</em> fire — an unusable measurement.</summary>
    public required ImmutableSortedSet<string> CanariesSilent { get; init; }

    public ImmutableArray<Finding> FindingsFor(string ruleId) => [
        .. Findings.Where(finding => string.Equals(finding.RuleId, ruleId, StringComparison.Ordinal))
    ];

    /// <summary>What this run is entitled to say about <paramref name="ruleId" />.</summary>
    public CorpusVerdict Verdict(string ruleId) {
        if (FindingsFor(ruleId).Length > 0) {
            return CorpusVerdict.Fired;
        }

        if (CanariesFired.Contains(ruleId)) {
            return CorpusVerdict.Declined;
        }

        return CanariesSilent.Contains(ruleId) ? CorpusVerdict.Silent : CorpusVerdict.Unplanted;
    }
}

/// <summary>How many of the fixture tree's positives the harness itself still finds.</summary>
/// <remarks>
///     ⚠ This is the instrument's own calibration and it is asserted, not printed. If the mass compile
///     stops firing what it fired when the floor was pinned, every zero the harness produced afterwards
///     is meaningless — and a harness that has quietly stopped working reports the same clean sweep as
///     a corpus with nothing in it.
/// </remarks>
public sealed record CorpusRecall {
    public required int Fired { get; init; }

    public required int Total { get; init; }

    /// <summary>Positives excluded because they configure the analyzer through an EditorConfig key.</summary>
    public required int Configured { get; init; }

    public required int CompilerErrors { get; init; }

    public required ImmutableArray<string> AnalyzerFailures { get; init; }

    /// <summary>The positives that did not fire, as <c>SKxxxx/name</c>.</summary>
    public required ImmutableArray<string> Missed { get; init; }

    public double Fraction => Total == 0 ? 0 : (double)Fired / Total;
}

/// <summary>
///     <c>Testing/corpus/real/</c> as an instrument a <b>semantic</b> rule can be measured with.
/// </summary>
/// <remarks>
///     ⚠ <b>Issue #277.</b> docs/plan/08's shipping bar asks for zero false positives on the reference
///     trees, and for a <c>requiresSemantics</c> rule the ordinary routes answer nothing at all:
///     <list type="number">
///         <item>
///             <c>skala.jsonc</c> excludes <c>Testing/corpus/**</c>, so <c>skala check</c> over those
///             paths answers <c>SK9023: no C# files were found</c> and exits before a rule is loaded.
///         </item>
///         <item>
///             The trees carry no project file, so <c>--load=loose</c> <em>skips</em> every
///             <c>requiresSemantics</c> rule by design (<see cref="AnalyzerHost.SkippedFor" />).
///         </item>
///     </list>
///     This builds the compilation the trees never had: one per tree, over that tree's own sources,
///     against the running framework's reference set — the same references
///     <c>RuleFixtures.Compile</c> hands a rule — and runs the production analyzer host over it in a
///     mode that does not filter the semantic rules out.
///     <para>
///         ⚠ <b>Three things it must do or it is a worse instrument than no instrument.</b> It excludes
///         the oracle fixture twins (<see cref="Sources" />); it synthesises the SDK's implicit global
///         usings (<see cref="ImplicitGlobalUsings" />); and it reports its own compiler-error count,
///         its own <c>SK9030</c> count and its own recall beside every finding count.
///     </para>
/// </remarks>
public static class RuleCorpus {
    /// <summary>
    ///     A stand-in for the <c>ImplicitUsings</c> file the SDK generates into <c>obj/</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Its absence lies in both directions, which is why it is on by default here.</b>
    ///     Measured while shipping <c>SK1084</c>: without <c>System.Linq</c> in scope the rule reported
    ///     <b>0</b> over Vixen and <b>8 true positives</b> with it — a rule suppressed outright. And
    ///     measured while shipping <c>SK2134</c>: not one of Serilog's 70 vendored files carries
    ///     <c>using System;</c>, so <c>[ThreadStatic]</c> bound to an error symbol, the rule's
    ///     <c>[ThreadStatic]</c> exclusion never matched, and it reported <b>7</b> — false positives
    ///     from a missing using, which went to <b>2, both true</b> once the tree was added.
    ///     <para>
    ///         So the artefact both silences rules and defeats their exclusions, and either direction
    ///         produces a plausible number. <see cref="RuleAudit" /> keeps it opt-in because it audits
    ///         arbitrary trees; a corpus sweep is always over the same three, and for those the fair
    ///         model of the tree is the one with the usings in it.
    ///     </para>
    /// </remarks>
    public const string ImplicitGlobalUsings =
        """
        // Microsoft.NET.Sdk's default implicit global usings for C#. Synthesised, never committed as
        // a corpus file: it is not part of what the corpus measures, it is part of the instrument.
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    /// <summary>The path the synthesised usings tree is parsed under. Never reportable.</summary>
    public static string ImplicitGlobalUsingsPath { get; } =
        Path.Combine(Corpus.SetRoot(Corpus.Real), "__implicit__", "ImplicitGlobalUsings.cs");

    /// <summary>
    ///     The vendored trees, one compilation each.
    /// </summary>
    /// <remarks>
    ///     ⚠ One per tree rather than one over all three, because a single compilation over the three
    ///     collides: they are unrelated projects that both declare, for instance, their own
    ///     <c>StringBuilderCache</c>, and a duplicate declaration makes a semantic rule decline what it
    ///     cannot bind — turning every count into a floor.
    /// </remarks>
    public static IReadOnlyList<string> Trees() => [
        .. Sources().Select(static file => file.RelativePath.Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
    ];

    /// <summary>
    ///     The tree's own source files — ⚠ <b>not</b> its committed oracle fixtures.
    /// </summary>
    /// <remarks>
    ///     ⚠ The corpus keeps three copies of every file: <c>X.cs</c>, <c>X.expected.cs</c> and
    ///     <c>X.arranged.expected.cs</c>, so its 1 140 files are 380 sources times three. Compiling all
    ///     three produces thousands of spurious <c>CS0111</c>/<c>CS0101</c>/<c>CS0229</c> — every type
    ///     declared three times — and a semantic rule declines what it cannot bind, so every count taken
    ///     that way is a floor rather than a measurement. Measured by removing the exclusion and
    ///     re-running: the twins take the compiler-error count over the three trees from <b>15 738</b> to
    ///     <b>73 312</b>. ⚠ Issue #277 records 53 658 → 13 036 for the same comparison and those figures
    ///     do not reproduce here — they were taken over one synthetic project spanning all three trees
    ///     rather than three compilations, which is a different population. The multiplier survives; the
    ///     numbers do not.
    ///     <para>
    ///         ⚠ <see cref="Corpus.Files" /> already performs exactly that exclusion — one
    ///         <c>EndsWith(".expected.cs")</c> covers the <c>.arranged.</c> twin too — so this reuses it
    ///         rather than re-deriving a filter that could disagree with the one the fidelity numbers
    ///         are taken under.
    ///     </para>
    /// </remarks>
    public static IReadOnlyList<CorpusFile> Sources(string? tree = null) => [
        .. Corpus.Files(Corpus.Real)
            .Where(file => tree is null
                || file.RelativePath.StartsWith(tree + "/", StringComparison.Ordinal))
    ];

    /// <summary>
    ///     The rule's own first committed positive fixture, as a shape to plant.
    /// </summary>
    /// <remarks>
    ///     ⚠ Returns <c>null</c> rather than throwing when a rule has no positive fixture, because
    ///     "there is no canary" and "the canary did not fire" are opposite states and
    ///     <see cref="CorpusVerdict" /> keeps them apart.
    /// </remarks>
    public static PlantedShape? Canary(string ruleId) {
        var directory = Path.Combine(FixtureRoot, ruleId, "positive");
        if (!Directory.Exists(directory)) {
            return null;
        }

        var file = Directory.GetFiles(directory, "*.cs")
            .OrderBy(static path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        return file is null
            ? null
            : new PlantedShape(ruleId, Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));
    }

    /// <summary>
    ///     Compiler errors over one tree, without running a single analyzer.
    /// </summary>
    /// <remarks>
    ///     ⚠ The cheap half, so that the with/without-usings pair the sweep reports costs a bind rather
    ///     than a second analyzer run.
    /// </remarks>
    public static int CompilerErrors(string tree, bool implicitUsings, CancellationToken cancellation = default) {
        var (compilation, _) = Build(tree, implicitUsings, []);
        return compilation.GetDiagnostics(cancellation)
            .Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>Runs every Skala rule over one tree, with the planted shapes in the same compilation.</summary>
    public static CorpusSweepResult Sweep(
        string tree,
        IReadOnlyList<PlantedShape>? planted = null,
        bool implicitUsings = true,
        CancellationToken cancellation = default
    ) {
        var shapes = planted ?? [];
        var (compilation, reportable) = Build(tree, implicitUsings, shapes);
        var plantedPaths = shapes.ToDictionary(
            shape => Path.GetFullPath(shape.PathIn(TreeRoot(tree))),
            static shape => shape.RuleId,
            StringComparer.Ordinal
        );

        var outcome = Run(compilation, tree, reportable, cancellation);
        var findings = ImmutableArray.CreateBuilder<Finding>();
        var fired = ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
        var silent = ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
        var errors = 0;

        foreach (var finding in outcome.Findings) {
            if (finding.RuleId.StartsWith("CS", StringComparison.Ordinal)) {
                if (finding.Severity == SkalaSeverity.Error) {
                    errors++;
                }

                continue;
            }

            if (!finding.RuleId.StartsWith("SK", StringComparison.Ordinal)) {
                continue;
            }

            if (plantedPaths.TryGetValue(finding.Path, out var owner)) {
                if (string.Equals(owner, finding.RuleId, StringComparison.Ordinal)) {
                    fired.Add(owner);
                }

                continue;
            }

            findings.Add(finding);
        }

        foreach (var shape in shapes) {
            if (!fired.Contains(shape.RuleId)) {
                silent.Add(shape.RuleId);
            }
        }

        return new() {
            Tree = tree,
            Files = reportable.Count - shapes.Count,
            ImplicitUsings = implicitUsings,
            CompilerErrors = errors,
            AnalyzerFailures = Failures(outcome),
            Findings = findings.ToImmutable(),
            CanariesFired = fired.ToImmutable(),
            CanariesSilent = silent.ToImmutable()
        };
    }

    /// <summary>
    ///     The harness measured against the fixture tree: how many committed positives it still fires.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately the same route as <see cref="Sweep" /> — one compilation over many files, the
    ///     framework reference set, the synthesised usings — because what is being calibrated is the
    ///     mass compile, not the rules. <c>RuleFixtureTests</c> already compiles each fixture on its own
    ///     and that number is 100% by construction; the gap between the two <em>is</em> the harness's
    ///     error bar, and it comes from cross-file collisions in a compilation whose files were never
    ///     meant to be neighbours.
    ///     <para>
    ///         ⚠ Positives carrying a <c>// analyzer-option:</c> header are excluded and counted
    ///         separately. They fire only under an EditorConfig value the sweep does not supply, so
    ///         leaving them in would depress the recall for a reason that is about the fixture rather
    ///         than about the instrument.
    ///     </para>
    /// </remarks>
    public static CorpusRecall Recall(CancellationToken cancellation = default) {
        var trees = ImmutableArray.CreateBuilder<SyntaxTree>();
        var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        var configured = 0;

        trees.Add(Parse(ImplicitGlobalUsings, ImplicitGlobalUsingsPath));

        foreach (var (ruleId, path) in Positives()) {
            var text = File.ReadAllText(path);
            if (text.Contains("// analyzer-option:", StringComparison.Ordinal)) {
                configured++;
                continue;
            }

            var full = Path.GetFullPath(path);
            trees.Add(Parse(text, full));
            reportable.Add(full);
            expected[full] = ruleId;
        }

        var compilation = Create("recall", trees.ToImmutable());
        var outcome = Run(compilation, "recall", reportable.ToImmutable(), cancellation);
        var fired = new HashSet<string>(StringComparer.Ordinal);
        var errors = 0;

        foreach (var finding in outcome.Findings) {
            if (finding.RuleId.StartsWith("CS", StringComparison.Ordinal)) {
                if (finding.Severity == SkalaSeverity.Error) {
                    errors++;
                }

                continue;
            }

            if (expected.TryGetValue(finding.Path, out var ruleId)
                && string.Equals(ruleId, finding.RuleId, StringComparison.Ordinal)) {
                fired.Add(finding.Path);
            }
        }

        return new() {
            Fired = fired.Count,
            Total = expected.Count,
            Configured = configured,
            CompilerErrors = errors,
            AnalyzerFailures = Failures(outcome),
            Missed = [
                .. expected.Where(entry => !fired.Contains(entry.Key))
                    .Select(static entry => entry.Value + "/" + Path.GetFileNameWithoutExtension(entry.Key))
                    .OrderBy(static name => name, StringComparer.Ordinal)
            ]
        };
    }

    /// <summary>Every committed positive fixture, as <c>(ruleId, path)</c>.</summary>
    public static IReadOnlyList<(string RuleId, string Path)> Positives() {
        if (!Directory.Exists(FixtureRoot)) {
            return [];
        }

        var result = new List<(string, string)>();
        foreach (var directory in Directory.GetDirectories(FixtureRoot)
                     .OrderBy(static path => path, StringComparer.Ordinal)) {
            var positive = Path.Combine(directory, "positive");
            if (!Directory.Exists(positive)) {
                continue;
            }

            foreach (var file in Directory.GetFiles(positive, "*.cs")
                         .OrderBy(static path => path, StringComparer.Ordinal)) {
                result.Add((Path.GetFileName(directory), file));
            }
        }

        return result;
    }

    /// <summary>The sweep's whole output as text: the numbers, then the findings a person must read.</summary>
    public static string Report(IReadOnlyList<string>? rules = null, CancellationToken cancellation = default) {
        var builder = new StringBuilder();
        var shapes = (rules ?? [])
            .Select(Canary)
            .OfType<PlantedShape>()
            .ToList();

        var recall = Recall(cancellation);
        builder.Append("recall  ")
            .Append(recall.Fired.ToString(CultureInfo.InvariantCulture))
            .Append(" of ")
            .Append(recall.Total.ToString(CultureInfo.InvariantCulture))
            .Append(" committed positives fire under the mass compile (")
            .Append((recall.Fraction * 100).ToString("F1", CultureInfo.InvariantCulture))
            .Append("%), ")
            .Append(recall.CompilerErrors.ToString(CultureInfo.InvariantCulture))
            .Append(" CS error(s), ")
            .Append(recall.Configured.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" excluded as EditorConfig-configured.");
        Append(builder, "recall", recall.AnalyzerFailures);
        builder.AppendLine();

        foreach (var tree in Trees()) {
            var without = CompilerErrors(tree, false, cancellation);
            var result = Sweep(tree, shapes, true, cancellation);
            builder.Append(tree)
                .Append("  ")
                .Append(result.Files.ToString(CultureInfo.InvariantCulture))
                .Append(" file(s), ")
                .Append(result.CompilerErrors.ToString(CultureInfo.InvariantCulture))
                .Append(" CS error(s) with the implicit usings and ")
                .Append(without.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" without them.");
            Append(builder, tree, result.AnalyzerFailures);

            if (shapes.Count > 0) {
                builder.Append("  canaries: ")
                    .Append(result.CanariesFired.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" fired, ")
                    .Append(result.CanariesSilent.Count.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(
                        result.CanariesSilent.Count == 0
                            ? " silent."
                            : " silent — those rules' zeros say nothing: "
                            + string.Join(", ", result.CanariesSilent)
                    );
            }

            foreach (var group in result.Findings
                         .GroupBy(static finding => finding.RuleId, StringComparer.Ordinal)
                         .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
                builder.Append("  ")
                    .Append(group.Key)
                    .Append("  ")
                    .Append(group.Count().ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" finding(s)");
                foreach (var finding in group
                             .OrderBy(static finding => finding.Path, StringComparer.Ordinal)
                             .ThenBy(static finding => finding.Line)) {
                    builder.Append("      ")
                        .Append(Path.GetFileName(finding.Path))
                        .Append(':')
                        .Append(finding.Line.ToString(CultureInfo.InvariantCulture))
                        .Append("  ")
                        .AppendLine(finding.Message);
                }
            }

            foreach (var rule in rules ?? []) {
                builder.Append("  verdict ")
                    .Append(rule)
                    .Append(": ")
                    .AppendLine(result.Verdict(rule).ToString());
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    static string FixtureRoot { get; } = Path.Combine(
        Corpus.RepositoryRoot,
        "Rules",
        "Rikarin.Skala.Rules.Tests",
        "fixtures"
    );

    /// <summary>
    ///     ⚠ The rules that ship <c>defaultSeverity: none</c>, turned on for the sweep.
    /// </summary>
    /// <remarks>
    ///     Roslyn's severity filter drops a disabled rule's diagnostic before it reaches anybody, so
    ///     without this the sweep would report a clean zero for every opt-in rule and the zero would be
    ///     about the filter. <c>RuleFixtures</c> opts the same set in for the same reason.
    /// </remarks>
    static ImmutableDictionary<string, ReportDiagnostic> OptIn { get; } = BuildOptIn();

    static ImmutableDictionary<string, ReportDiagnostic> BuildOptIn() {
        var builder = ImmutableDictionary.CreateBuilder<string, ReportDiagnostic>(StringComparer.Ordinal);
        foreach (var rule in RuleCatalog.All) {
            if (!rule.Retired && rule.DefaultSeverity == RuleSeverity.None) {
                builder[rule.Id] = ReportDiagnostic.Warn;
            }
        }

        return builder.ToImmutable();
    }

    static string TreeRoot(string tree) => Path.Combine(Corpus.SetRoot(Corpus.Real), tree);

    static (CSharpCompilation Compilation, ImmutableHashSet<string> Reportable) Build(
        string tree,
        bool implicitUsings,
        IReadOnlyList<PlantedShape> planted
    ) {
        var trees = ImmutableArray.CreateBuilder<SyntaxTree>();
        var reportable = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

        if (implicitUsings) {
            // ⚠ Parsed into the compilation and left out of `reportable`: it is part of the
            // instrument, so a finding on it would be the harness measuring itself.
            trees.Add(Parse(ImplicitGlobalUsings, ImplicitGlobalUsingsPath));
        }

        foreach (var file in Sources(tree)) {
            var full = Path.GetFullPath(file.Path);
            trees.Add(Parse(File.ReadAllText(file.Path), full));
            reportable.Add(full);
        }

        foreach (var shape in planted) {
            var full = Path.GetFullPath(shape.PathIn(TreeRoot(tree)));
            trees.Add(Parse(shape.Source, full));
            reportable.Add(full);
        }

        return (Create(tree, trees.ToImmutable()), reportable.ToImmutable());
    }

    static SyntaxTree Parse(string text, string path) =>
        CSharpSyntaxTree.ParseText(
            SourceText.From(text),
            new CSharpParseOptions(LanguageVersion.Preview).WithDocumentationMode(DocumentationMode.Parse),
            path
        );

    static CSharpCompilation Create(string name, ImmutableArray<SyntaxTree> trees) =>
        CSharpCompilation.Create(
            name,
            trees,

            // ⚠ `SharedFrameworkReferences` is what `--load=loose` gives a rule, so the sweep asks a
            // rule for no more than the product already does. The alternative — restoring each
            // vendored tree's real dependency closure — is what issue #277 rejected as changing what
            // the formatter corpus is for.
            SharedFrameworkReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable,
                concurrentBuild: true,
                specificDiagnosticOptions: OptIn
            )
        );

    /// <summary>
    ///     The production analyzer host, told this is not a loose load.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="LoadMode.Binlog" /> over a compilation that came from no build, and it is the
    ///     one thing this harness does that the product must never do. Under
    ///     <see cref="LoadMode.Loose" /> <see cref="AnalyzerHost" /> drops every
    ///     <c>requiresSemantics</c> rule — correctly, because a rule that answers "no finding" through
    ///     an unresolved symbol makes a clean report mean two things. The sweep needs those rules to
    ///     run, and pays for it with the canary and the recall floor rather than with a promise.
    ///     <para>
    ///         ⚠ <c>new AnalyzerOptions([])</c> rather than the repository's <c>.editorconfig</c> chain.
    ///         Skala's own <c>.editorconfig</c> carries 253 <c>dotnet_diagnostic</c> severities; letting
    ///         it reach this compilation would make the corpus's answer a fact about Skala's
    ///         configuration, and a severity edit would silently move a number nobody re-measured.
    ///     </para>
    /// </remarks>
    static AnalysisOutcome Run(
        CSharpCompilation compilation,
        string name,
        ImmutableHashSet<string> reportable,
        CancellationToken cancellation
    ) =>
        AnalyzerHost.Run(
            new CompilationUnit { Name = name, Compilation = compilation, ReportablePaths = reportable },
            new AnalyzerOptions([]),
            [],
            LoadMode.Binlog,
            cancellation
        );

    /// <summary>⚠ <c>SK9030</c> and any <c>AD0001</c> that reached a finding, as messages.</summary>
    static ImmutableArray<string> Failures(AnalysisOutcome outcome) => [
        .. outcome.Diagnostics
            .Where(static diagnostic => diagnostic.Id is "SK9030")
            .Select(static diagnostic => diagnostic.Message),
        .. outcome.Findings
            .Where(static finding => finding.RuleId is "AD0001")
            .Select(static finding => finding.Message)
    ];

    static void Append(StringBuilder builder, string label, ImmutableArray<string> failures) {
        foreach (var failure in failures) {
            builder.Append("  ⚠ ").Append(label).Append(": ").AppendLine(failure);
        }
    }
}
