using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Testing;

public enum FuzzMode {
    /// <summary>Take a corpus file and apply parse-preserving mutations to it.</summary>
    Mutate,

    /// <summary>Build a random syntax tree from the weighted grammar and print it with random whitespace.</summary>
    Generate,

    /// <summary>Both, drawn per case.</summary>
    Both
}

/// <summary>What a run was asked to do. ⚠ Everything a case does is a function of its seed.</summary>
public sealed record FuzzOptions {
    public ulong Seed { get; init; } = 1;

    /// <summary>
    ///     The wall-clock budget, which is what doc 12 asks the nightly job to be bounded by.
    /// </summary>
    /// <remarks>
    ///     ⚠ A time budget rather than a case count, deliberately, and it is the one place the clock is
    ///     allowed to reach the fuzzer. It bounds the *loop*; it does not enter a case. Case <c>i</c> is
    ///     <c>FuzzRandom.Derive(seed, i)</c> and nothing else, so a run that stopped after 11 042 cases
    ///     on a fast machine and 3 118 on a slow one executed the same first 3 118 cases, and any of
    ///     them replays from its own seed in a second.
    /// </remarks>
    public TimeSpan Budget { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>An exact case count, which overrides <see cref="Budget" /> when set.</summary>
    public long? Cases { get; init; }

    public FuzzMode Mode { get; init; } = FuzzMode.Both;

    /// <summary>
    ///     Run the arrange-and-format pair on one case in every <c>n</c>. Zero turns it off.
    /// </summary>
    /// <remarks>
    ///     ⚠ Sampled rather than universal because it costs a Roslyn compilation with the whole shared
    ///     framework referenced, which is tens of times a format. Running it on every case would buy one
    ///     property at the price of an order of magnitude of throughput, and a fuzzer's yield is roughly
    ///     linear in cases executed.
    /// </remarks>
    public int ArrangeEvery { get; init; } = 25;

    public bool Minimise { get; init; } = true;

    /// <summary>Where minimised findings are written. ⚠ Not <c>corpus/pathological/</c>; see the driver.</summary>
    public string? OutputDirectory { get; init; }

    public int Parallelism { get; init; } = Math.Max(1, Environment.ProcessorCount - 1);
}

/// <summary>One case, reconstructed from its seed alone.</summary>
public sealed record FuzzCase(
    ulong Seed,
    FuzzMode Kind,
    string Origin,
    string Path,
    string Baseline,
    string Text,
    ImmutableArray<Mutation> Mutations,
    bool AbsorbedOnly);

/// <summary>One property that did not hold, with the input that broke it.</summary>
public sealed record FuzzFinding(
    ulong Seed,
    string Origin,
    string Kind,
    PropertyViolation Violation,
    ImmutableArray<string> Mutations,
    string Source,
    string Minimised,
    string MinimisedDetail) {
    public string Property => Violation.Property;
}

/// <summary>What a run covered, which is the half of a fuzz report that is read when nothing failed.</summary>
public sealed record FuzzReport(
    ulong Seed,
    FuzzMode Mode,
    long Cases,
    TimeSpan Elapsed,
    long CasesThatChangedSomething,
    long Edits,
    long GeneratedUnits,
    long ArrangementChecks,
    IReadOnlyDictionary<string, long> MutationsApplied,
    IReadOnlyDictionary<string, long> Violations,
    IReadOnlyList<string> CorpusFilesTouched,
    long ParseLost,
    ImmutableArray<ulong> ParseLostSeeds,
    ImmutableArray<FuzzFinding> Findings) {
    public string Render() {
        var report = new StringBuilder();
        var seconds = Math.Max(Elapsed.TotalSeconds, 0.001);
        report.AppendLine("# fuzz");
        report.AppendLine();
        report.AppendLine(
            $"seed {FuzzRandom.Format(Seed)}, mode {Mode.ToString().ToLowerInvariant()}, "
            + $"{Cases.ToString("N0", CultureInfo.InvariantCulture)} cases in "
            + $"{Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s "
            + $"({(Cases / seconds).ToString("F0", CultureInfo.InvariantCulture)} cases/s)"
        );

        report.AppendLine();
        report.AppendLine("## what it covered");
        report.AppendLine();
        report.AppendLine(
            $"- {CasesThatChangedSomething.ToString("N0", CultureInfo.InvariantCulture)} of "
            + $"{Cases.ToString("N0", CultureInfo.InvariantCulture)} cases produced at least one edit "
            + $"({Percent(CasesThatChangedSomething, Cases)}), {Edits.ToString("N0", CultureInfo.InvariantCulture)} edits in total"
        );

        report.AppendLine(
            $"- {CorpusFilesTouched.Count.ToString("N0", CultureInfo.InvariantCulture)} distinct corpus files mutated, "
            + $"{GeneratedUnits.ToString("N0", CultureInfo.InvariantCulture)} compilation units generated"
        );

        report.AppendLine(
            $"- {ArrangementChecks.ToString("N0", CultureInfo.InvariantCulture)} cases also ran the arrange-and-format pair"
        );

        // ⚠ Reported, and reported with seeds to replay. A mutation that breaks the parse produces a
        // file ADR-003 leaves byte-identical, so every property holds over it for free: parse-lost
        // cases are cases that asserted nothing, and a fuzzer that hides its own dead weight is
        // reporting a case count it did not earn.
        report.AppendLine(
            $"- {ParseLost.ToString("N0", CultureInfo.InvariantCulture)} property checks lost the parse — "
            + "a fuzzer defect, not a formatter one; those cases asserted nothing"
            + (ParseLostSeeds.IsEmpty
                    ? string.Empty
                    : " (e.g. " + string.Join(", ", ParseLostSeeds.Select(FuzzRandom.Format)) + ")")
        );

        report.AppendLine();
        report.AppendLine("| mutation | applied |");
        report.AppendLine("|---|---:|");
        foreach (var entry in MutationsApplied.OrderByDescending(static e => e.Value)
                     .ThenBy(static e => e.Key, StringComparer.Ordinal)) {
            report.AppendLine($"| `{entry.Key}` | {entry.Value.ToString("N0", CultureInfo.InvariantCulture)} |");
        }

        report.AppendLine();
        report.AppendLine("## what it found");
        report.AppendLine();
        if (Violations.Count == 0) {
            report.AppendLine("Nothing. Every property held over every case.");
        } else {
            report.AppendLine("| property | violations |");
            report.AppendLine("|---|---:|");
            foreach (var entry in Violations.OrderByDescending(static e => e.Value)
                         .ThenBy(static e => e.Key, StringComparer.Ordinal)) {
                report.AppendLine($"| `{entry.Key}` | {entry.Value.ToString("N0", CultureInfo.InvariantCulture)} |");
            }
        }

        if (!Findings.IsEmpty) {
            report.AppendLine();
            foreach (var finding in Findings.Take(25)) {
                report.AppendLine(
                    $"### {finding.Property} — seed {FuzzRandom.Format(finding.Seed)} ({finding.Origin})"
                );

                report.AppendLine();
                report.AppendLine($"- mutations: {string.Join(", ", finding.Mutations)}");
                report.AppendLine(
                    $"- minimised from {finding.Source.Length.ToString("N0", CultureInfo.InvariantCulture)} to "
                    + $"{finding.Minimised.Length.ToString("N0", CultureInfo.InvariantCulture)} characters"
                );

                report.AppendLine($"- as found: {finding.Violation}");
                report.AppendLine($"- minimised: {finding.MinimisedDetail}");

                report.AppendLine(
                    $"- replay: `dotnet run --project Testing/Rikarin.Skala.Testing -- fuzz --replay={FuzzRandom.Format(finding.Seed)}`"
                );
                report.AppendLine();
                report.AppendLine("```csharp");
                report.AppendLine(Visible(finding.Minimised));
                report.AppendLine("```");
                report.AppendLine();
            }
        }

        return report.ToString();
    }

    static string Percent(long part, long whole) =>
        whole == 0 ? "0 %" : (part * 100.0 / whole).ToString("F1", CultureInfo.InvariantCulture) + " %";

    /// <summary>
    ///     The artefact with its invisible characters made visible.
    /// </summary>
    /// <remarks>
    ///     ⚠ A fuzz finding is very often *about* a character that a code block does not render: a
    ///     trailing space, a tab where the file uses spaces, a lone `\r`, a BOM. Printing the artefact
    ///     raw produces a report where the interesting difference is invisible and the reader concludes
    ///     the tool is confused.
    /// </remarks>
    static string Visible(string text) =>
        text.Replace("﻿", "<BOM>", StringComparison.Ordinal)
            .Replace("\r", "<CR>", StringComparison.Ordinal)
            .Replace("\t", "<TAB>", StringComparison.Ordinal)
            .Replace(" \n", "<SP>\n", StringComparison.Ordinal)
            .TrimEnd('\n');
}

/// <summary>
///     The fuzzer of docs/plan/12 § "4. Fuzzing".
/// </summary>
/// <remarks>
///     ⚠ M7 installed the nightly job and did not write this, and the workflow's header said so rather
///     than implying otherwise by existing. What the job ran was the *assertion half*: the six properties
///     over the committed corpus, which is a regression suite. The three things that were missing are
///     here — a seeded mutation driver (<see cref="FuzzMutations" />), a weighted generative grammar
///     (<see cref="FuzzGenerator" />) and a delta-debugging minimiser (<see cref="FuzzMinimiser" />) — and
///     the reason to want them is that every defect the milestones actually found was one a fixed corpus
///     could not see:
///     <list type="bullet">
///         <item>
///             M3: two of the fitter's four measures had returned zero since the milestone they were
///             written in, and no property caught it.
///         </item>
///         <item>M3: a non-idempotency no corpus file contains, which took a 4 708-file tree to surface.</item>
///         <item>
///             M4: <c>PredefinedTypeRule</c> rewriting <c>out var value</c> into <c>out string value</c> on
///             2 210 of 4 606 files, hidden because a later rule mostly re-converted the damage.
///         </item>
///         <item>
///             M8: for a category the corpus has none of the shape of, a correct rule and a rule that never
///             runs produce the same zero.
///         </item>
///     </list>
/// </remarks>
public static class Fuzzer {
    /// <summary>
    ///     The path a generated unit is formatted as.
    /// </summary>
    /// <remarks>
    ///     ⚠ It does not exist and does not need to; <see cref="OptionResolver" /> walks directories for
    ///     <c>.editorconfig</c> files and does not require the file itself. Putting it under the corpus
    ///     root means a generated unit is formatted under exactly the configuration a corpus file is,
    ///     which is what makes the two halves of the fuzzer comparable.
    /// </remarks>
    public static string GeneratedPath { get; } = Path.Combine(Corpus.Root, "generated", "fuzz.cs");

    static readonly ConcurrentDictionary<string, FormattingOptions> OptionCache = new(StringComparer.Ordinal);

    /// <summary>Reconstructs a case from its seed. ⚠ This is what makes a nightly failure actionable.</summary>
    public static FuzzCase Build(ulong seed, FuzzMode mode, IReadOnlyList<CorpusFile> corpus) {
        var random = new FuzzRandom(seed);
        var kind = mode is FuzzMode.Both
            ? random.Chance(0.65) ? FuzzMode.Mutate : FuzzMode.Generate
            : mode;

        string origin;
        string path;
        string baseline;

        if (kind is FuzzMode.Mutate && corpus.Count > 0) {
            var file = corpus[random.Next(corpus.Count)];
            origin = file.ToString();
            path = file.Path;
            baseline = File.ReadAllText(file.Path);
        } else {
            kind = FuzzMode.Generate;
            origin = "generated";
            path = GeneratedPath;

            // ⚠ doc 12: "print them with random whitespace". The printing is the same
            // parse-preserving mutation catalogue the other half uses, applied to the grammar's
            // canonical output — two implementations of "where may whitespace go" is one more than
            // the number that can be kept correct.
            baseline = FuzzGenerator.Compile(random);
            var prints = random.Next(1, 5);
            for (var i = 0; i < prints; i++) {
                var printed = FuzzMutations.Apply(baseline, random, Corpus.PropertySymbols, PrintNames);
                if (printed is not null) {
                    baseline = printed.Text;
                }
            }
        }

        // ⚠ A case is either whitespace-only or it is not, and the choice is made up front. Mixing
        // the two would mean the absorption property — the strongest one here — could never be
        // asserted, because a single inserted comment makes the output legitimately different.
        var absorbedOnly = random.Chance(0.42);
        IReadOnlyList<string>? names = absorbedOnly ? FuzzMutations.AbsorbedNames : null;
        var count = random.Next(1, absorbedOnly ? 7 : 6);
        var text = baseline;
        var mutations = ImmutableArray.CreateBuilder<Mutation>();
        for (var i = 0; i < count; i++) {
            var mutation = FuzzMutations.Apply(text, random, Corpus.PropertySymbols, names);
            if (mutation is null) {
                continue;
            }

            mutations.Add(mutation);
            text = mutation.Text;
        }

        return new FuzzCase(seed, kind, origin, path, baseline, text, mutations.ToImmutable(), absorbedOnly);
    }

    /// <summary>The mutations a generated unit is "printed" with.</summary>
    static readonly ImmutableArray<string> PrintNames = [
        FuzzMutations.Indent,
        FuzzMutations.Tabs,
        FuzzMutations.WidenGap,
        FuzzMutations.TrailingSpace,
        FuzzMutations.SplitLine,
        FuzzMutations.JoinLine,
        FuzzMutations.BlankLines,
        FuzzMutations.LineEndings
    ];

    public static FormattingOptions OptionsFor(string path) =>
        OptionCache.GetOrAdd(
            Path.GetDirectoryName(path) ?? path,
            _ => OptionResolver.Resolve(path).Options
        );

    /// <summary>Runs one case and returns what it did and what it broke.</summary>
    public static (ImmutableArray<PropertyViolation> Violations, int Edits) Execute(
        FuzzCase subject,
        bool arrangement,
        FormatSaboteur? saboteur = null,
        CancellationToken cancellation = default
    ) {
        var options = OptionsFor(subject.Path);

        // ⚠ The absorption baseline is `format(x)` under each symbol set, computed only when the
        // whole mutation sequence was whitespace. A formatter-off span opts a case out, for the
        // reason PropertyTests records: a verbatim region is not whitespace, it is data, and a
        // mutation inside one is not something the formatter is allowed to absorb.
        //
        // ⚠ The tag itself is spelled out in the string literal below and deliberately **not** in
        // this comment. `CSharpDocumentBuilder.ContainsTag` is a plain `Contains` over a comment's
        // text, so a comment that merely *mentions* the tag turns formatting off from that point to
        // the end of the file — and with SK-FUZZ-0005 open, that made `./build.sh Lint` refuse to
        // format this file at all. See `corpus/pathological/open/register.md`.
        (string None, string Defined)? baseline = null;
        if (subject.AbsorbedOnly
            && !subject.Baseline.Contains("@formatter:off", StringComparison.Ordinal)) {
            baseline = (
                FuzzProperties.Format(subject.Path, subject.Baseline, options, [], saboteur),
                FuzzProperties.Format(subject.Path, subject.Baseline, options, Corpus.PropertySymbols, saboteur)
            );
        }

        var violations = FuzzProperties.Check(
            subject.Path,
            subject.Text,
            options,
            Corpus.PropertySymbols,
            baseline,
            arrangement,
            saboteur,
            cancellation
        );

        // ⚠ Guarded, and it was not: the coverage number is a *measurement*, and a measurement that
        // can take the process down with it loses the whole run's report — which is what happened
        // the first time this fuzzer was pointed at `corpus/real/` and the formatter threw an
        // IndexOutOfRangeException out of EditEmitter. The properties themselves already record a
        // throw as `crash`; this call must not be the one that escapes.
        try {
            var edits = Rikarin.Skala.Formatting.CSharp.CSharpFormatter.Format(
                subject.Path,
                Microsoft.CodeAnalysis.Text.SourceText.From(subject.Text),
                options,
                null,
                []
            ).Edits.Length;

            return (violations, edits);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            return (violations, 0);
        }
    }

    /// <summary>The run.</summary>
    public static FuzzReport Run(FuzzOptions options, TextWriter log, CancellationToken cancellation = default) {
        var corpus = Corpus.All();
        var clock = Stopwatch.StartNew();
        var mutations = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        var violations = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        var touched = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var findings = new ConcurrentBag<(long Index, FuzzFinding Finding)>();
        long cases = 0;
        long changed = 0;
        long edits = 0;
        long generated = 0;
        long arranged = 0;
        long parseLost = 0;
        var parseLostSeeds = new ConcurrentBag<ulong>();

        // ⚠ Reported per property rather than per case, and only once per property. A single
        // non-idempotency in a common construct fires on thousands of cases, and a report that lists
        // all of them is a report nobody reads to the end. The first of each is minimised; the rest
        // are counted.
        var seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        var batch = Math.Max(options.Parallelism * 4, 8);
        for (long start = 0; !cancellation.IsCancellationRequested; start += batch) {
            if (options.Cases is { } limit && start >= limit) {
                break;
            }

            if (options.Cases is null && clock.Elapsed >= options.Budget) {
                break;
            }

            var size = options.Cases is { } total ? (int)Math.Min(batch, total - start) : batch;
            Parallel.For(
                0,
                size,
                new ParallelOptions { MaxDegreeOfParallelism = options.Parallelism, CancellationToken = cancellation },
                offset => {
                    var index = start + offset;
                    var seed = FuzzRandom.Derive(options.Seed, index);
                    FuzzCase subject;
                    try {
                        subject = Build(seed, options.Mode, corpus);
                    } catch (Exception exception) when (exception is not OperationCanceledException) {
                        // A generator or mutation defect. Counted, never silent, never a formatter
                        // finding.
                        mutations.AddOrUpdate("fuzzer:" + exception.GetType().Name, 1, static (_, value) => value + 1);
                        return;
                    }

                    Interlocked.Increment(ref cases);
                    if (subject.Kind is FuzzMode.Generate) {
                        Interlocked.Increment(ref generated);
                    } else {
                        touched.TryAdd(subject.Origin, 0);
                    }

                    foreach (var mutation in subject.Mutations) {
                        mutations.AddOrUpdate(mutation.Name, 1, static (_, value) => value + 1);
                    }

                    var arrangement = options.ArrangeEvery > 0 && index % options.ArrangeEvery == 0;
                    if (arrangement) {
                        Interlocked.Increment(ref arranged);
                    }

                    var (found, caseEdits) = Execute(subject, arrangement, null, cancellation);
                    Interlocked.Add(ref edits, caseEdits);
                    if (caseEdits > 0) {
                        Interlocked.Increment(ref changed);
                    }

                    foreach (var violation in found) {
                        if (violation.Property == FuzzProperties.ParseLost) {
                            Interlocked.Increment(ref parseLost);
                            if (parseLostSeeds.Count < 5) {
                                parseLostSeeds.Add(seed);
                            }

                            continue;
                        }

                        violations.AddOrUpdate(violation.Property, 1, static (_, value) => value + 1);
                        if (!seen.TryAdd(violation.Property, 0)) {
                            continue;
                        }

                        log.WriteLine($"  ✗ {violation.Property} at seed {FuzzRandom.Format(seed)} ({subject.Origin})");
                        findings.Add((index, Report(subject, violation, options)));
                    }
                }
            );
        }

        clock.Stop();
        return new FuzzReport(
            options.Seed,
            options.Mode,
            cases,
            clock.Elapsed,
            changed,
            edits,
            generated,
            arranged,
            mutations.ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal),
            violations.ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal),
            [.. touched.Keys.Order(StringComparer.Ordinal)],
            parseLost,
            [.. parseLostSeeds.Order().Take(5)],
            [.. findings.OrderBy(static f => f.Index).Select(static f => f.Finding)]
        );
    }

    /// <summary>Minimises a failure and, if asked, writes it out.</summary>
    static FuzzFinding Report(FuzzCase subject, PropertyViolation violation, FuzzOptions options) {
        // ⚠ Absorption minimises the *pre-mutation* text, every other property the mutated text, and
        // the asymmetry is the property's own shape. "format(mutate_whitespace(x)) ≡ format(x)" is a
        // statement about a pair, and there is no single string that carries it — so the artefact is
        // x, and the predicate re-derives the mutation from the case seed on each candidate.
        var artefact = violation.Property == FuzzProperties.Absorption ? subject.Baseline : subject.Text;
        var minimised = artefact;
        if (options.Minimise) {
            var budget = new MinimiseBudget(6000);
            minimised = FuzzMinimiser.Minimise(
                artefact,
                candidate => Fails(subject, candidate, violation.Property),
                budget
            );
        }

        // ⚠ The violation is re-read off the *minimised* input, not carried over from the case that
        // found it. A detail like "the second pass still wants 1 edit: [2990..2990) -> \r" names an
        // offset in a 2 494-character file that no longer exists, which is a fact about the run
        // rather than about the bug; the same sentence over the 38-character reduction is the bug.
        var minimisedDetail = Describe(subject, minimised, violation.Property);

        var finding = new FuzzFinding(
            subject.Seed,
            subject.Origin,
            subject.Kind.ToString().ToLowerInvariant(),
            violation,
            [.. subject.Mutations.Select(static m => m.Name)],
            artefact,
            minimised,
            minimisedDetail
        );

        if (options.OutputDirectory is { Length: > 0 } directory) {
            Directory.CreateDirectory(directory);
            var name = $"fuzz-{violation.Property}-{FuzzRandom.Format(subject.Seed)}.cs";

            // ⚠ Byte for byte, with no trailing trim. A trailing space, a missing final newline and
            // a lone `\r` are all things this fuzzer finds, and a writer that tidies the artefact
            // before saving it is a writer that throws the finding away on the way to disk.
            File.WriteAllText(Path.Combine(directory, name), minimised);
            File.WriteAllText(
                Path.Combine(directory, Path.ChangeExtension(name, ".txt")),
                $"seed {FuzzRandom.Format(subject.Seed)}\norigin {subject.Origin}\n"
                + $"mutations {string.Join(", ", subject.Mutations.Select(static m => m.Name))}\n"
                + $"as found: {violation}\nminimised: {minimisedDetail}\n"
            );
        }

        return finding;
    }

    /// <summary>How the property fails on one particular input, in the words the report prints.</summary>
    static string Describe(FuzzCase subject, string candidate, string property) =>
        Violations(subject, candidate, property).FirstOrDefault()?.ToString()
        ?? "⚠ the minimised input no longer exhibits the failure; the reduction is not trustworthy";

    /// <summary>Does <paramref name="candidate" /> still break <paramref name="property" />?</summary>
    static bool Fails(FuzzCase subject, string candidate, string property) =>
        Violations(subject, candidate, property).Any();

    static IEnumerable<PropertyViolation> Violations(FuzzCase subject, string candidate, string property) {
        var options = OptionsFor(subject.Path);
        if (property != FuzzProperties.Absorption) {
            // ⚠ The arrangement half is off by default and has to be asked for, or the predicate
            // for an arrangement finding never runs the pipeline that produced it and answers "no
            // longer fails" on every candidate — including the unreduced original.
            var arrangement = property
                is FuzzProperties.ArrangementIdempotency
                    or FuzzProperties.ArrangementConvergence;

            return FuzzProperties
                .Check(subject.Path, candidate, options, Corpus.PropertySymbols, arrangement: arrangement)
                .Where(violation => violation.Property == property);
        }

        // The absorption predicate: is there a whitespace-only mutation of this candidate, derived
        // from this case's own seed, that the formatter does not absorb?
        //
        // ⚠ The case's whole *sequence* first, in order, off one stream — not one mutation from a
        // fresh one. Absorption is very often a composition: an `indent` that widens a line past the
        // margin and a `widen-gap` that widens it further do together what neither does alone, and a
        // predicate that replays one of them reports "no longer fails" on an input that fails.
        // Falling back to the individual mutations afterwards keeps the reduction going once the
        // candidate has shrunk past the sequence's foothold, because what is being pinned is the
        // smallest input that breaks the *property*, not the one that breaks one mutation.
        var sequence = candidate;
        var stream = new FuzzRandom(subject.Seed);
        foreach (var mutation in subject.Mutations) {
            if (FuzzMutations.Apply(mutation.Name, sequence, stream, Corpus.PropertySymbols) is { } step) {
                sequence = step;
            }
        }

        var attempts = new List<string> { sequence };
        foreach (var name in FuzzMutations.AbsorbedNames) {
            if (FuzzMutations.Apply(name, candidate, new FuzzRandom(subject.Seed), Corpus.PropertySymbols) is { } one) {
                attempts.Add(one);
            }
        }

        foreach (var mutated in attempts) {
            if (string.Equals(mutated, candidate, StringComparison.Ordinal)) {
                continue;
            }

            var baseline = (
                FuzzProperties.Format(subject.Path, candidate, options, []),
                FuzzProperties.Format(subject.Path, candidate, options, Corpus.PropertySymbols)
            );

            var found = FuzzProperties
                .Check(subject.Path, mutated, options, Corpus.PropertySymbols, baseline)
                .Where(static violation => violation.Property == FuzzProperties.Absorption)
                .ToArray();

            if (found.Length > 0) {
                return found;
            }
        }

        return [];
    }

    /// <summary>
    ///     <c>fuzz --grammar-check</c>: does the generator emit C# that parses?
    /// </summary>
    /// <remarks>
    ///     ⚠ The generator's contract is "no parse errors, semantic nonsense welcome", and this is how
    ///     the contract is checked rather than assumed. A production that emits a parse error costs more
    ///     than the case it wastes: ADR-003 leaves an unparseable file byte-identical, so the case still
    ///     *passes* every property, and a grammar that is 40 % broken looks exactly like a grammar that
    ///     is 0 % broken from the outside. The histogram below names the diagnostic and shows the line,
    ///     which is what turns "the generator is broken" into "the generator parenthesises nothing".
    /// </remarks>
    public static string GrammarCheck(ulong seed, int count) {
        var errors = new Dictionary<string, (int Count, string Sample)>(StringComparer.Ordinal);
        var broken = 0;
        for (var i = 0; i < count; i++) {
            var source = FuzzGenerator.Compile(new FuzzRandom(FuzzRandom.Derive(seed, i)));
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(source),
                Rikarin.Skala.Formatting.CSharp.CSharpFormatter.ParseOptions
            );

            var reported = tree.GetDiagnostics()
                .Where(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .ToArray();

            if (reported.Length == 0) {
                continue;
            }

            broken++;
            foreach (var diagnostic in reported.Take(2)) {
                var line = diagnostic.Location.GetLineSpan().StartLinePosition.Line;
                var text = source.ReplaceLineEndings("\n").Split('\n');
                var sample = line < text.Length ? text[line].Trim() : string.Empty;
                var key = diagnostic.Id + " " + diagnostic.GetMessage(CultureInfo.InvariantCulture);
                errors[key] = errors.TryGetValue(key, out var existing)
                    ? (existing.Count + 1, existing.Sample)
                    : (1, sample.Length > 140 ? sample[..140] + "…" : sample);
            }
        }

        var report = new StringBuilder();
        report.AppendLine(
            $"{broken.ToString(CultureInfo.InvariantCulture)} of {count.ToString(CultureInfo.InvariantCulture)} "
            + "generated units have a parse error."
        );

        report.AppendLine();
        foreach (var entry in errors.OrderByDescending(static e => e.Value.Count).Take(20)) {
            report.AppendLine($"{entry.Value.Count.ToString(CultureInfo.InvariantCulture),5}  {entry.Key}");
            report.AppendLine($"       {entry.Value.Sample}");
        }

        return report.ToString();
    }

    /// <summary>
    ///     <c>fuzz --mutation-test</c>: break the formatter deliberately and check that the fuzzer notices.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the answer to "a fuzzer that finds nothing on its first outing is more likely to be
    ///     weak than the code to be perfect". Each saboteur in
    ///     <see cref="FuzzProperties.Saboteurs" /> breaks one property; this runs cases until the property
    ///     that should notice does, and reports the case count. A saboteur that survives the whole budget
    ///     is a property that is not being asserted, and the row says so.
    /// </remarks>
    public static string MutationTest(FuzzOptions options, TextWriter log) {
        var corpus = Corpus.All();
        var report = new StringBuilder();
        report.AppendLine("| saboteur | breaks | caught by | after |");
        report.AppendLine("|---|---|---|---:|");
        var missed = 0;

        foreach (var saboteur in FuzzProperties.Saboteurs) {
            var caught = false;
            long index;
            var limit = options.Cases ?? 400;
            for (index = 0; index < limit && !caught; index++) {
                var subject = Build(FuzzRandom.Derive(options.Seed, index), options.Mode, corpus);
                var (violations, _) = Execute(subject, arrangement: false, saboteur);
                foreach (var violation in violations) {
                    if (violation.Property == saboteur.Target) {
                        report.AppendLine(
                            $"| `{saboteur.Name}` | `{saboteur.Target}` | ✓ | "
                            + $"{index.ToString("N0", CultureInfo.InvariantCulture)} cases |"
                        );

                        caught = true;
                        break;
                    }
                }
            }

            if (!caught) {
                missed++;
                report.AppendLine(
                    $"| `{saboteur.Name}` | `{saboteur.Target}` | **✗ NOT CAUGHT** | "
                    + $"{index.ToString("N0", CultureInfo.InvariantCulture)} cases |"
                );
            }

            log.WriteLine($"  {saboteur.Name}: {(caught ? "caught" : "MISSED")}");
        }

        report.AppendLine();
        report.AppendLine(
            missed == 0
                ? "Every saboteur was caught by the property it breaks: the mutations reach the formatter and the "
                + "oracle reads the answer."
                : $"⚠ {missed.ToString(CultureInfo.InvariantCulture)} saboteur(s) survived. A property no saboteur "
                + "can trip is a property that is not being asserted."
        );

        return report.ToString();
    }
}
