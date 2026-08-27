using System.Globalization;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

// The harness's own entry point. ⚠ Every one of these is a deliberate developer-machine action and
// none of them is a test:
//
//   oracle [set…]     regenerate the committed `.expected.cs` fixtures from `jb cleanupcode`.
//                     `./build.sh Oracle`, never a test — an oracle that regenerates when it
//                     disagrees is a tautology (docs/plan/12 § "The oracle").
//   fidelity [set…]   print the differential report without failing anything, which is the work
//                     queue the divergence classes rank.
//   xmldoc [set]      what the doc-comment sub-formatter costs against an oracle profile that
//                     does not run ReSharper's own doc-comment task (SK-DIV-0006): the number
//                     with the sub-formatter and without it, over every line and over the lines
//                     outside doc comments.
//   dump <set> <dir> [defined]
//                     write Skala's output and the oracle's side by side, so a class named in the
//                     report can be read as a diff rather than as two sample lines. `defined`
//                     supplies the oracle's own preprocessor symbols, which is the run the
//                     conformance bar is read against.
//   variants [set…]   the differential number for each alternative configuration a set is run
//                     under — docs/plan/05's four-way keep_existing_* table.
//   constructs [set]  every divergent line attributed to the construct that owns it, beside how
//                     often that construct occurs — docs/plan/16 § R1's actual question.
//   ask <dir>         run the oracle over a scratch directory, in place. The tool the milestone-3
//                     wrapping rules were established with: an option name does not say what
//                     happens to a 121-column array initializer, and asking does.
//   audit [dir…] [--implicit-usings]
//                     every rule's findings over a tree, grouped by rule, for the
//                     false-positive review docs/plan/16 § R3 makes the shipping bar.
//                     ⚠ `--implicit-usings` supplies the global-usings file the SDK writes into
//                     obj/, which the loader skips. A tree that sets `ImplicitUsings` binds
//                     `Dictionary<,>` to an error type without it and most of the semantic rule
//                     set goes quiet for the wrong reason.
//   sample <tree> <n> <dest>
//                     redraw a corpus sample from a tree, reproducibly: the file is chosen by a
//                     hash of its path rather than by a seeded sequence, so the same commit and
//                     the same filters give the same files on any machine.
//   tree <dir> [n]    the differential over an arbitrary tree rather than over the corpus: what
//                     the oracle would move, what Skala would move, and Skala against the oracle
//                     over all of it. Tens of minutes.
//   locate <set> <kind>
//                     the divergent lines attributed to one construct, with file and line. R1
//                     counts constructs rather than lines, so a construct with two divergent lines
//                     is as far from the rule as one with ninety and the ranked report never shows
//                     it.
//   margin [out]      SK-DIV-0005's constant, swept: eleven right-hand-side shapes at five block
//                     depths under both values of `wrap_before_eq`, one character at a time.
//   fuzz [flags]      docs/plan/12 § "4. Fuzzing", as a program. Seeded mutation of the corpus and
//                     a weighted generative grammar, both asserted against the seven properties
//                     under both symbol sets, with a delta-debugging minimiser behind any failure.
//                     ⚠ Bounded by a time budget rather than a case count, and every case is a
//                     function of its seed alone: `--replay=<seed>` reconstructs one exactly.
//                       --seed=N          the root seed. Default 1; the nightly job passes the run id.
//                       --minutes=N       the wall-clock budget. Default 2.
//                       --cases=N         an exact case count instead of a budget.
//                       --mode=…          mutate | generate | both (default).
//                       --arrange-every=N run the arrange-and-format pair on one case in N. 0 is off.
//                       --out=DIR         write minimised findings there. Default `.skala/fuzz/`.
//                       --no-minimise     report the raw failing input instead of shrinking it.
//                       --replay=SEED     re-execute one case and print it.
//                       --mutation-test   break the formatter deliberately, per property, and
//                                         report which property caught it and after how many cases.
//   unformat […]      the differential over *degraded* input, with the null hypothesis beside every
//                     number. `report` reads committed fixtures; `regenerate` re-degrades the
//                     sample and re-runs the oracle over it, and is a reviewed action.
//                     ⚠ `generate` on its own DELETES the fixtures with the inputs — the two are
//                     one artefact — so it is `unformat oracle` or nothing after it.
//   preprocessor      SK-DIV-0004's number: `corpus/real/` fidelity with the oracle's own
//                     preprocessor symbols supplied, split by whether the file contains a `#if`.
//                     The symbols are read out of a real binary log rather than typed.
//   defaults [out]    derive ReSharper's built-in default table from the oracle, because nobody
//                     publishes it (docs/plan/03 § "Deriving ReSharper's defaults"). Tens of
//                     minutes.
//
// It is not the `skala` tool and is never packaged.

if (args.Length == 0) {
    Console.Error.WriteLine(
        "usage: oracle [set…] | fidelity [set…] | constructs [set…] | dump <set> <dir>"
        + " | ask <dir> | defaults [round…] | preprocessor [symbol…] | audit [dir…]"
        + " | fuzz [--seed=N] [--minutes=N] [--cases=N] [--mode=…] [--replay=SEED] [--mutation-test]"
    );
    return 2;
}

var sets = args.Length > 1
    ? args[1..].Where(static argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray()
    : [Corpus.Constructs, Corpus.Real, Corpus.Pathological];

if (sets.Length == 0) {
    sets = [Corpus.Constructs, Corpus.Real, Corpus.Pathological];
}

// ⚠ `oracle <set> --only=<prefix>` regenerates the fixtures of the files whose relative path starts
// with a prefix, rather than the whole set. A fixture regeneration is a reviewed commit, and a
// commit that rewrites 273 files because four were added is not reviewable — the diff is the review.
var only = args.FirstOrDefault(static argument => argument.StartsWith("--only=", StringComparison.Ordinal))
    ?["--only=".Length..];

switch (args[0]) {
    case "oracle":
        return Regenerate(sets, only);
    case "fidelity":
        return Report(sets);
    case "xmldoc":
        // ⚠ SK-DIV-0006's assertion, measured. Four numbers: the oracle agreement with the
        // sub-formatter and without it, each over every line and over the lines outside doc
        // comments. The last pair is the one that says whether anything the sub-formatter is not
        // allowed to touch has moved.
        Console.Write(XmlDocFidelity.Measure(sets[0]));
        return 0;
    case "dump":
        return Dump(args[1], args[2], args.Length > 3 && args[3] == "defined");
    case "variants":
        return Variants(sets);
    case "probe":
        return Probe();
    case "ask":
        return Ask(args[1], args[2..]);
    case "defaults":
        return Defaults(args.Length > 1 ? args[1] : null);
    case "audit": {
        // docs/plan/16 § R3's shipping bar, as a list a person can read: every rule's findings over
        // a tree, grouped, so that "zero false positives" is checked rather than claimed.
        //
        // ⚠ `--implicit-usings` supplies the global-usings file the SDK generates into obj/, which
        // the loader skips. Without it a tree that sets `ImplicitUsings` binds `Dictionary<,>` to an
        // error type and most of the semantic rule set goes quiet for the wrong reason — over Vixen
        // it is the difference between 195 724 errors and 128 833. Off by default because
        // `corpus/real/` predates implicit usings and adding them there moves recorded numbers.
        var auditPaths = args[1..]
            .Where(static argument => !argument.StartsWith("--", StringComparison.Ordinal))
            .ToArray();

        Console.WriteLine(
            RuleAudit.Run(
                auditPaths.Length > 0 ? auditPaths : [Corpus.SetRoot(Corpus.Real)],
                true,
                Array.IndexOf(args, "--implicit-usings") >= 0
            )
        );

        return 0;
    }
    case "preprocessor":
        // SK-DIV-0004, measured. The symbol set comes from a real binary log of the same project
        // the oracle's fixtures were produced under, read through the loader `skala check` uses.
        Console.WriteLine(
            PreprocessorFidelity.Measure(
                args.Length > 1 && args[1] != "-" ? args[1..] : PreprocessorFidelity.OracleSymbols(Console.Out)
            )
        );

        return 0;
    case "sample":
        // ⚠ Redraws a corpus sample from a tree, reproducibly. `sample <tree> <count> <dest>`.
        // A deliberate action whose output is reviewed in its own commit, like `oracle`: it
        // replaces the corpus, and a corpus that changes without a commit is not a measurement.
        if (args.Length < 4) {
            Console.Error.WriteLine("usage: sample <tree> <count> <destination>");
            return 2;
        }

        Console.WriteLine(
            CorpusSample.Draw(
                Path.GetFullPath(args[1]),
                int.Parse(args[2], CultureInfo.InvariantCulture),
                Path.GetFullPath(args[3]),
                Console.Error
            )
        );

        return 0;
    case "margin":
        // SK-DIV-0005, swept. A developer action of tens of seconds; never a test.
        if (OracleRunner.FindExecutableOrNull() is null) {
            Console.Error.WriteLine("jb is not installed.");
            return 2;
        }

        var sweep = MarginSweep.Run(
            new OracleRunner(),
            Path.Combine(Corpus.RepositoryRoot, ".editorconfig"),
            Console.Error
        );

        if (args.Length > 1) {
            File.WriteAllText(args[1], sweep);
            Console.WriteLine($"written to {args[1]}");
        } else {
            Console.WriteLine(sweep);
        }

        return 0;
    case "tree":
        // ⚠ The differential over an arbitrary tree rather than over the corpus. `tree <dir>`.
        // The corpus samples 200 files of Vixen; this measures all 4 711, which is the number the
        // `.editorconfig` commit is actually decided on — "how many files would Rider move back"
        // is a question about the tree, not about a sample of it.
        if (args.Length < 2) {
            Console.Error.WriteLine("usage: tree <directory>");
            return 2;
        }

        return TreeFidelity(
            Path.GetFullPath(args[1]),
            args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : int.MaxValue
        );
    case "fuzz":
        return Fuzz(args[1..]);
    case "arrangement":
        // ⚠ M4's bar: the oracle's cleanup profile against Skala's arrange-and-format pipeline, per
        // changed span rather than per line. `--aggressive` turns on parenthesis removal, which the
        // oracle's profile does and Skala's default deliberately does not, so running both ways
        // prices the gate rather than hiding it.
        return Arrangement(args[1..]);
    case "arrange-tree":
        // ⚠ M4's second bar: arrangement over a whole tree introduces zero compiler diagnostics.
        // `arrange-tree <dir> [--load=binlog|workspace|loose] [--aggressive] [--limit=N]`.
        // Read-only — the caller supplies a `git archive` scratch copy and this never writes.
        if (args.Length < 2) {
            Console.Error.WriteLine("usage: arrange-tree <directory> [--load=mode] [--aggressive] [--limit=N]");
            return 2;
        }

        var treeMode = args.FirstOrDefault(static a => a.StartsWith("--load=", StringComparison.Ordinal))
            ?["--load=".Length..]
            ?? "binlog";

        // `--explain=<path fragment>`: which rule, run alone, makes the re-bind reject this file.
        if (args.FirstOrDefault(static a => a.StartsWith("--explain=", StringComparison.Ordinal)) is { } explain) {
            Console.WriteLine(
                ArrangeTree.Explain(Path.GetFullPath(args[1]), treeMode, explain["--explain=".Length..], Console.Error)
            );

            return 0;
        }

        var treeLimit = args.FirstOrDefault(static a => a.StartsWith("--limit=", StringComparison.Ordinal)) is { } l
            ? int.Parse(l["--limit=".Length..], CultureInfo.InvariantCulture)
            : int.MaxValue;

        Console.WriteLine(
            ArrangeTree.Run(
                Path.GetFullPath(args[1]),
                treeMode,
                args.Contains("--aggressive"),
                treeLimit,
                Console.Error
            )
                .Render()
        );

        return 0;
    case "unformat":
        // ⚠ The differential over *degraded* input — the measurement the 99.63 % headline was
        // missing. `corpus/real/`'s inputs are already 90.95 % line-identical to their fixtures, so
        // that number sits on a 91 % floor and the whole discriminating power of it lives in the
        // other 9 %. This degrades a file's formatting, runs both tools over the degraded copy and
        // compares them, with the null hypothesis reported beside every number.
        //   unformat report                the measurement, from committed fixtures. No jb needed.
        //   unformat generate [--count=N]  redraw and re-degrade the corpus. ⚠ Deletes the fixtures.
        //   unformat oracle                the fixtures for whatever is committed. Needs jb.
        //   unformat regenerate [--count=N]  both, in order.
        return UnformatCommand(args[1..]);
    case "locate":
        // Where the divergent lines attributed to one construct are. `locate <set> <kind>`.
        if (args.Length < 3) {
            Console.Error.WriteLine("usage: locate <set> <SyntaxKind>");
            return 2;
        }

        Console.WriteLine(ConstructReport.Locate(args[1], args[2], Symbols()));
        return 0;
    case "constructs":
        // docs/plan/16 § R1: any construct occurring more than 50 times must be at 100 %. A single
        // fidelity number cannot answer that; this attributes every divergent line to the construct
        // that owns it and puts it beside how often the construct occurs.
        Console.WriteLine(
            ConstructReport.Render(ConstructReport.Build(args.Length > 1 ? args[1] : Corpus.Real, Symbols()))
        );
        return 0;
    default:
        Console.Error.WriteLine($"unknown command '{args[0]}'");
        return 2;
}

// `ask <dir> [--profile=NAME] [key=value…]`: run the oracle over a scratch directory of .cs files,
// in place, under the repository's .editorconfig plus any overrides.
//
// ⚠ `--profile=SkalaCleanup` asks the *arrangement* half, and it is not decoration: the two profiles
// answer different questions and a question asked of the wrong one is worse than not asking. The
// formatter-tag measurement in SK-DIV-0016 is exactly that — CSReformatCode honours the tags and the
// cleanup profile does not, and nothing but running both would have shown it.
//
// ⚠ It is the tool the milestone-3 rules were established with, and it is why they are rules rather
// than readings of an option name. `wrap_array_initializer_style = wrap_if_long` does not say what
// happens to `new[] { a, b, c }` at 121 columns; asking cleanupcode does. It is also what derives
// the default table (docs/plan/03 § "Deriving ReSharper's defaults"), where the override is
// `root = true` and nothing else.
static int Ask(string directory, string[] overrides) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine("jb is not installed.");
        return 2;
    }

    var full = Path.GetFullPath(directory);
    var files = Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories)
        .OrderBy(static path => path, StringComparer.Ordinal)
        .Select(path => new CorpusFile("ask", Path.GetRelativePath(full, path), path))
        .ToArray();

    if (files.Length == 0) {
        Console.Error.WriteLine($"no .cs files under {full}");
        return 2;
    }

    var pairs = new List<KeyValuePair<string, string>>();
    var config = Path.Combine(Corpus.RepositoryRoot, ".editorconfig");
    var profile = OracleProfile.FormatOnly;
    foreach (var entry in overrides) {
        if (entry.StartsWith("--config=", StringComparison.Ordinal)) {
            config = Path.GetFullPath(entry["--config=".Length..]);
            continue;
        }

        if (entry.StartsWith("--profile=", StringComparison.Ordinal)) {
            profile = OracleProfile.ByName(entry["--profile=".Length..])
                ?? throw new ArgumentException($"no such oracle profile: {entry["--profile=".Length..]}");

            continue;
        }

        var equals = entry.IndexOf('=', StringComparison.Ordinal);
        if (equals > 0) {
            pairs.Add(new KeyValuePair<string, string>(entry[..equals].Trim(), entry[(equals + 1)..].Trim()));
        }
    }

    var results = new OracleRunner().Format(files, config, pairs, profile);
    foreach (var file in files) {
        if (results.TryGetValue(file.Path, out var body)) {
            File.WriteAllText(file.Path, body);
        }
    }

    Console.WriteLine(
        $"{results.Count.ToString(CultureInfo.InvariantCulture)}/{files.Length.ToString(CultureInfo.InvariantCulture)} files answered."
    );
    return 0;
}

// `defaults [out]`: derive ReSharper's built-in defaults from the oracle, because nobody publishes
// them (docs/plan/03 § "Deriving ReSharper's defaults"). Tens of minutes; a deliberate developer
// action, never a test.
static int Defaults(string? outputPath) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine("jb is not installed.");
        return 2;
    }

    var probed = DefaultsProbe.Run(new OracleRunner(), Console.Out);
    var report = DefaultsProbe.Render(probed);
    if (outputPath is { Length: > 0 }) {
        File.WriteAllText(outputPath, report);
        Console.WriteLine($"written to {outputPath}");
    } else {
        Console.WriteLine(report);
    }

    return 0;
}

// `fuzz [flags]`: docs/plan/12 § "4. Fuzzing".
//
// ⚠ The exit code is 1 when a property did not hold, so the nightly job fails rather than uploading
// a green report with a finding buried in it. It is 0 when the run found nothing — which is a claim
// the report is required to back up with coverage numbers, because a fuzzer whose mutations never
// reach the formatter also finds nothing and looks identical from the outside.
static int Fuzz(string[] args) {
    string? Flag(string name) =>
        args.FirstOrDefault(argument => argument.StartsWith("--" + name + "=", StringComparison.Ordinal))
            ?[(name.Length + 3)..];

    var options = new FuzzOptions {
        Seed = Flag("seed") is { } seed ? FuzzRandom.Parse(seed) : 1,
        Budget = TimeSpan.FromMinutes(
            Flag("minutes") is { } minutes ? double.Parse(minutes, CultureInfo.InvariantCulture) : 2
        ),
        Cases = Flag("cases") is { } cases ? long.Parse(cases, CultureInfo.InvariantCulture) : null,
        Mode = Flag("mode") switch {
            "mutate" => FuzzMode.Mutate,
            "generate" => FuzzMode.Generate,
            _ => FuzzMode.Both
        },
        ArrangeEvery = Flag("arrange-every") is { } every
            ? int.Parse(every, CultureInfo.InvariantCulture)
            : 25,
        Minimise = !args.Contains("--no-minimise"),
        Parallelism = Flag("jobs") is { } jobs
            ? int.Parse(jobs, CultureInfo.InvariantCulture)
            : Math.Max(1, Environment.ProcessorCount - 1),
        OutputDirectory = Flag("out") ?? Path.Combine(Corpus.RepositoryRoot, ".skala", "fuzz")
    };

    // ⚠ `--replay=<seed>` reconstructs one case from its seed and nothing else. This is the whole
    // point of the SplitMix64 stream in FuzzRandom: a seed recorded in a nightly log six months ago
    // rebuilds the same bytes today, on any runtime, on any platform.
    if (Flag("replay") is { } replay) {
        var subject = Fuzzer.Build(FuzzRandom.Parse(replay), options.Mode, Corpus.All());
        Console.WriteLine(
            $"seed {FuzzRandom.Format(subject.Seed)} — {subject.Kind.ToString().ToLowerInvariant()} of {subject.Origin}"
        );

        Console.WriteLine(
            $"mutations: {string.Join(", ", subject.Mutations.Select(mutation => mutation.Name))}"
            + (subject.AbsorbedOnly ? " (whitespace only — absorption is asserted)" : string.Empty)
        );

        var (violations, edits) = Fuzzer.Execute(subject, options.ArrangeEvery > 0);
        Console.WriteLine(
            $"the formatter wanted {edits.ToString(CultureInfo.InvariantCulture)} edit(s) on it; "
            + $"{violations.Length.ToString(CultureInfo.InvariantCulture)} property violation(s)."
        );

        foreach (var violation in violations) {
            Console.WriteLine("  ✗ " + violation);
        }

        // ⚠ Both halves, when the caller asks. An absorption failure is a statement about a *pair*
        // — `format(mutate(x))` against `format(x)` — and printing only the mutated half leaves the
        // reader with one of the two files the finding is about.
        if (options.OutputDirectory is { Length: > 0 } into && args.Contains("--dump")) {
            Directory.CreateDirectory(into);
            File.WriteAllText(Path.Combine(into, "replay-baseline.cs"), subject.Baseline);
            File.WriteAllText(Path.Combine(into, "replay-mutated.cs"), subject.Text);
            Console.Error.WriteLine($"baseline and mutated input written to {into}");
        }

        Console.WriteLine();
        Console.WriteLine(subject.Text);
        return violations.Any(violation => violation.Property != FuzzProperties.ParseLost) ? 1 : 0;
    }

    // ⚠ `--check=<path>` asserts the seven properties over one file, read byte for byte. It is what
    // turns a minimised artefact into something a person can argue with: the artefact is a file, the
    // question is "does this file still break the property", and asking it should not require
    // reconstructing a fuzz case around it.
    if (Flag("check") is { } target) {
        var full = Path.GetFullPath(target);
        var found = FuzzProperties.Check(
            full,
            File.ReadAllText(full),
            Fuzzer.OptionsFor(full),
            Corpus.PropertySymbols,
            arrangement: options.ArrangeEvery > 0
        );

        foreach (var violation in found) {
            Console.WriteLine("  ✗ " + violation);
        }

        Console.WriteLine(
            found.IsEmpty
                ? "every property holds."
                : $"{found.Length.ToString(CultureInfo.InvariantCulture)} violation(s)."
        );

        return found.IsEmpty ? 0 : 1;
    }

    // ⚠ `--minimise=<path>` delta-debugs a file that already fails, without a fuzz case around it.
    // The findings that arrive from outside the fuzzer — a crash a user reports, a file `./build.sh
    // Lint` refuses — deserve the same reduction as the ones it finds itself, and reducing C# by
    // hand is a morning.
    if (Flag("minimise") is { } failing) {
        var full = Path.GetFullPath(failing);
        var resolved = Fuzzer.OptionsFor(full);

        bool Fails(string candidate) =>
            FuzzProperties
                .Check(full, candidate, resolved, Corpus.PropertySymbols, arrangement: options.ArrangeEvery > 0)
                .Any(violation => Flag("property") is not { } wanted
                    || string.Equals(violation.Property, wanted, StringComparison.Ordinal)
                );

        var original = File.ReadAllText(full);
        if (!Fails(original)) {
            Console.Error.WriteLine($"{full} does not violate anything; there is nothing to minimise.");
            return 2;
        }

        var budget = new MinimiseBudget(20000);
        var reduced = FuzzMinimiser.Minimise(original, Fails, budget);
        Console.Error.WriteLine(
            $"{original.Length.ToString(CultureInfo.InvariantCulture)} → "
            + $"{reduced.Length.ToString(CultureInfo.InvariantCulture)} characters in "
            + $"{budget.Used.ToString(CultureInfo.InvariantCulture)} evaluations"
        );

        foreach (var violation in FuzzProperties.Check(full, reduced, resolved, Corpus.PropertySymbols)) {
            Console.Error.WriteLine("  ✗ " + violation);
        }

        Console.Write(reduced);
        return 0;
    }

    if (args.Any(argument => argument.StartsWith("--grammar-check", StringComparison.Ordinal))) {
        Console.WriteLine(
            Fuzzer.GrammarCheck(
                options.Seed,
                Flag("grammar-check") is { Length: > 0 } sample
                    ? int.Parse(sample, CultureInfo.InvariantCulture)
                    : 500
            )
        );

        return 0;
    }

    if (args.Contains("--mutation-test")) {
        Console.WriteLine(Fuzzer.MutationTest(options, Console.Error));
        return 0;
    }

    Console.Error.WriteLine(
        $"fuzzing from seed {FuzzRandom.Format(options.Seed)}, "
        + (options.Cases is { } total
                ? total.ToString(CultureInfo.InvariantCulture) + " cases"
                : options.Budget.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + " minutes")
        + $", mode {options.Mode.ToString().ToLowerInvariant()}…"
    );

    var report = Fuzzer.Run(options, Console.Error);
    Console.WriteLine(report.Render());

    try {
        var directory = Path.Combine(Corpus.RepositoryRoot, ".skala");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "fuzz.md"), report.Render());
    } catch (IOException) {
        // The written report is a convenience; a read-only tree does not fail the run.
    }

    return report.Findings.IsEmpty ? 0 : 1;
}

// `arrangement [--aggressive] [--all-rules] [set…]`: the M4 differential.
static int Arrangement(string[] args) {
    var aggressive = args.Contains("--aggressive");

    // ⚠ The default excludes the three rewrites the oracle will not perform at all
    // (docs/oracle-cleanup-profile.md). `--all-rules` includes them, which is how the cost of that
    // exclusion is a number rather than an assertion.
    var filter = args.Contains("--all-rules")
        ? Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementFilter.All
        : Rikarin.Skala.Formatting.CSharp.Arrangement.ArrangementFilter.OracleComparable;

    var names = args.Where(static argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
    var files = names.Length > 0
        ? names.SelectMany(Corpus.Files).ToArray()
        : Corpus.Arrangeable();

    var withFixtures = files.Where(static file => file.HasFixtureFor(OracleProfile.Cleanup)).ToArray();
    if (withFixtures.Length == 0) {
        Console.Error.WriteLine("no cleanup fixtures. Run `./build.sh Oracle` (it now regenerates both profiles).");
        return 2;
    }

    Console.WriteLine(
        $"{withFixtures.Length.ToString(CultureInfo.InvariantCulture)} files with a cleanup fixture"
        + (aggressive ? ", --aggressive" : "")
        + (args.Contains("--all-rules") ? ", all rules" : ", oracle-comparable rules only")
    );

    // `--dump=<dir>` writes Skala's arrangement and the oracle's side by side, so a class named in
    // the report can be read as a diff rather than as two sample lines — the same affordance `dump`
    // gives the formatter's differential.
    if (args.FirstOrDefault(static a => a.StartsWith("--dump=", StringComparison.Ordinal)) is { } dumpArg) {
        var directory = Path.GetFullPath(dumpArg["--dump=".Length..]);
        Directory.CreateDirectory(directory);
        var compilation = ArrangementDifferential.Compile(withFixtures);
        foreach (var file in withFixtures) {
            var name = (file.Set + "_" + file.RelativePath).Replace('/', '_');
            File.WriteAllText(
                Path.Combine(directory, name + ".skala"),
                TextNormalisation.Normalise(ArrangementDifferential.Run(file, compilation, aggressive, filter).Text)
            );

            File.WriteAllText(
                Path.Combine(directory, name + ".oracle"),
                TextNormalisation.Normalise(OracleFixture.Read(file, OracleProfile.Cleanup))
            );
        }

        Console.WriteLine($"written to {directory}");
        return 0;
    }

    var report = ArrangementDifferential.Measure(withFixtures, aggressive, filter, Console.Error);
    Console.WriteLine(report.Render(10));

    foreach (var origin in withFixtures.GroupBy(
                 static file => file.Set + "/" + file.RelativePath.Split('/')[0],
                 StringComparer.Ordinal
             )
                 .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
        var slice = ArrangementDifferential.Measure(origin.ToArray(), aggressive, filter);
        Console.WriteLine(
            $"  {origin.Key,-28} {slice.Agreement * 100:F2} %  ({slice.Agreed.ToString(CultureInfo.InvariantCulture)}/{slice.Spans.ToString(CultureInfo.InvariantCulture)} spans, {slice.Files.ToString(CultureInfo.InvariantCulture)} files)"
        );
    }

    return 0;
}

// `unformat …`: docs/plan/12 § "The unformat differential".
//
// ⚠ `generate` and `oracle` are deliberate developer actions whose diffs are reviewed in their own
// commit, exactly like `sample` and `oracle`. `report` reads the committed fixtures and needs
// nothing installed, because that is what makes the number reproducible on a machine that has never
// had ReSharper on it (ADR-011).
static int UnformatCommand(string[] arguments) {
    var command = arguments.FirstOrDefault(static a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "report";
    var count = arguments.FirstOrDefault(static a => a.StartsWith("--count=", StringComparison.Ordinal)) is { } given
        ? int.Parse(given["--count=".Length..], CultureInfo.InvariantCulture)
        : UnformatCorpus.SampleSize;

    switch (command) {
        case "report":
            Console.Write(UnformatDifferential.Render(Symbols()));
            return 0;

        case "generate":
            Console.Write(UnformatCorpus.Generate(count, Console.Error));
            return 0;

        case "oracle":
        case "regenerate":
            if (OracleRunner.FindExecutableOrNull() is null) {
                Console.Error.WriteLine(
                    "jb is not installed. `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`."
                );

                return 2;
            }

            if (command == "regenerate") {
                Console.Write(UnformatCorpus.Generate(count, Console.Error));
            }

            var total = UnformatDifferential.Regenerate(
                new OracleRunner(),
                Path.Combine(Corpus.RepositoryRoot, ".editorconfig"),
                Console.Out
            );

            Console.WriteLine($"{total.ToString(CultureInfo.InvariantCulture)} fixtures written.");
            return 0;

        default:
            Console.Error.WriteLine("usage: unformat [report|generate|oracle|regenerate] [--count=N]");
            return 2;
    }
}

static int Regenerate(string[] sets, string? only) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine(
            "jb is not installed. `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`."
        );
        return 2;
    }

    var runner = new OracleRunner();
    var version = runner.Version;
    var editorConfig = Path.Combine(Corpus.RepositoryRoot, ".editorconfig");
    var hash = OracleFixture.HashConfig(editorConfig);
    var header = new OracleHeader(version, hash, OracleRunner.Profile, OracleFixture.Today);
    Console.WriteLine($"oracle: resharper={version} config=sha256:{hash} profile={OracleRunner.Profile}");

    var written = 0;
    foreach (var set in sets) {
        var files = Only(Corpus.Files(set), only);
        if (files.Count == 0) {
            continue;
        }

        // In batches: cleanupcode holds the whole project in memory and a corpus-sized one is slow.
        const int batch = 60;
        for (var start = 0; start < files.Count; start += batch) {
            var slice = files.Skip(start).Take(batch).ToArray();
            var results = runner.Format(slice, editorConfig);
            foreach (var file in slice) {
                if (results.TryGetValue(file.Path, out var body)) {
                    OracleFixture.Write(file, body, header);
                    written++;
                }
            }

            Console.WriteLine(
                $"  {set}: {Math.Min(start + batch, files.Count).ToString(CultureInfo.InvariantCulture)}/{files.Count.ToString(CultureInfo.InvariantCulture)}"
            );
        }
    }

    written += RegenerateVariants(runner, editorConfig, header, sets);
    written += RegenerateCleanup(runner, editorConfig, version, hash, sets, only);
    Console.WriteLine($"{written.ToString(CultureInfo.InvariantCulture)} fixtures written.");
    return 0;
}

// ⚠ The second profile's fixtures, and only for the files that have something to say under it
// (Corpus.Arrangeable): all of corpus/real/, plus constructs/arrangement/. A cleanup fixture beside
// every whitespace construct would cost 250 oracle runs to commit 250 files whose content is
// predictable from the format-only fixture beside them, which is coverage-shaped and measures
// nothing.
static int RegenerateCleanup(
    OracleRunner runner,
    string editorConfig,
    string version,
    string hash,
    string[] sets,
    string? only
) {
    var profile = OracleProfile.Cleanup;
    var header = new OracleHeader(version, hash, profile.Name, OracleFixture.Today);
    var wanted = new HashSet<string>(sets, StringComparer.Ordinal);
    var files = Only([.. Corpus.Arrangeable().Where(file => wanted.Contains(file.Set))], only).ToArray();
    if (files.Length == 0) {
        return 0;
    }

    Console.WriteLine(
        $"oracle: profile={profile.Name} over {files.Length.ToString(CultureInfo.InvariantCulture)} files"
    );

    // ⚠ ONE project holding every file, laid out at its own relative path — not the 60-file batches
    // of flattened `F0.cs … F59.cs` the format-only profile uses.
    //
    // Formatting is per-file and batching is free. Arrangement is not: `var`, target-typed `new` and
    // using removal are all questions about a compilation, and a batch is a different compilation
    // from the corpus. Measured, before this was fixed: `JObject o = JObject.Parse(json)` in
    // Newtonsoft's tests did not convert to `var`, because `JObject`'s own declaration sits in a file
    // that landed in a different batch and so did not resolve — while `string json` in the statement
    // above it converted, because `string` needs no resolution. The result was 130 spans of
    // "skala only" that were not disagreements at all; they were the harness measuring its own
    // batching. Flattening the names would have the same effect for a different reason: two corpus
    // files with the same type name in the same directory collide.
    var scratch = Directory.CreateTempSubdirectory("skala-cleanup-");
    try {
        File.Copy(editorConfig, Path.Combine(scratch.FullName, ".editorconfig"));
        File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), OracleRunner.ProjectFile);
        File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), OracleRunner.SolutionFile);

        var produced = new Dictionary<string, CorpusFile>(StringComparer.Ordinal);
        foreach (var file in files) {
            var target = Path.Combine(
                scratch.FullName,
                file.Set,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar)
            );

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file.Path, target, overwrite: true);
            produced[target] = file;
        }

        var results = runner.FormatInPlace(scratch.FullName, [.. produced.Keys], profile);
        var written = 0;
        foreach (var (target, file) in produced) {
            if (results.TryGetValue(target, out var body)) {
                OracleFixture.Write(file, profile, body, header);
                written++;
            }
        }

        Console.WriteLine($"  cleanup: {written.ToString(CultureInfo.InvariantCulture)} written");
        return written;
    } finally {
        try {
            scratch.Delete(recursive: true);
        } catch (IOException) {
            // A scratch directory the tool still holds open is not worth failing a build over.
        }
    }
}

// The files of a set whose relative path starts with `--only=`, or all of them when it was absent.
static IReadOnlyList<CorpusFile> Only(IReadOnlyList<CorpusFile> files, string? prefix) =>
    prefix is null
        ? files
        : [.. files.Where(file => file.RelativePath.StartsWith(prefix, StringComparison.Ordinal))];

// ⚠ The fixture sets that are measured under configurations other than the repository's, which is
// docs/plan/05's four-way keep_existing_* table. Each variant is a separate cleanupcode run with the
// overrides appended to the copied .editorconfig, and its output lands in
// `<file>.<variant>.expected.cs`.
static int RegenerateVariants(OracleRunner runner, string editorConfig, OracleHeader header, string[] sets) {
    var written = 0;
    foreach (var set in sets) {
        var byVariant = CorpusVariants.Pairs(set)
            .GroupBy(static pair => pair.Variant, static pair => pair.File);

        foreach (var group in byVariant) {
            var files = group.ToArray();
            var results = runner.Format(files, editorConfig, group.Key.Overrides);
            foreach (var file in files) {
                if (results.TryGetValue(file.Path, out var body)) {
                    OracleFixture.Write(file, group.Key, body, header);
                    written++;
                }
            }

            Console.WriteLine(
                $"  {set} [{group.Key.Name}]: {files.Length.ToString(CultureInfo.InvariantCulture)} files"
            );
        }
    }

    return written;
}

// ⚠ The differential runs under BOTH symbol sets, and that is the default rather than a flag.
// Milestone 5 found a defect that had survived M1 → M5 — `count > (n)` came back `count >(n)` —
// because every corpus line that shows it sits inside a `#if` body the formatter could not see. A
// single-symbol-set run cannot find that class at all, and there is no reason to think it was the
// only one, so the last section of this report is the divergences that appear under one symbol set
// and not the other. Those are the interesting kind.
static int Report(string[] sets) {
    var symbols = Symbols();
    foreach (var set in sets) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        if (files.Length == 0) {
            continue;
        }

        var bare = new List<(string File, string Expected, string Actual)>(files.Length);
        var defined = new List<(string File, string Expected, string Actual)>(files.Length);
        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var options = Resolve(file.Path);
            var expected = OracleFixture.Read(file);
            bare.Add((file.ToString(), expected, CSharpFormatter.Format(file.Path, text, options).Formatted));
            defined.Add(
                (file.ToString(), expected, CSharpFormatter.Format(file.Path, text, options, null, symbols).Formatted)
            );
        }

        var without = Fidelity.Compare(bare);
        var with = Fidelity.Compare(defined);

        Console.WriteLine($"── {set} ──────────────────────────────────────────────────────────");
        Console.WriteLine("                    line      file      lines");
        Console.WriteLine($"  basis: {without.BasisName}");
        Row("no symbols", without);
        Row("with symbols", with);

        // ⚠ Both bases, always, and the second one is not decoration. The ratchet excludes `///`
        // lines because Skala formats documentation comments and the pinned oracle profile does
        // not (SK-DIV-0006); an excluded category that is never printed is an excluded category
        // that can grow unwatched. docs/plan/12 § "A ratchet compares numbers over the same
        // population".
        var everyLine = Fidelity.Compare(bare, FidelityBasis.EveryLine);
        Row("no symbols, " + everyLine.BasisName, everyLine);
        Console.WriteLine();

        foreach (var origin in defined.GroupBy(static r => r.File.Split('/')[1], StringComparer.Ordinal)
                     .OrderBy(static g => g.Key, StringComparer.Ordinal)) {
            var report = Fidelity.Compare(origin);
            Console.WriteLine(
                $"  {origin.Key,-14} line {report.LineFidelity * 100:F2}%  file {report.FileFidelity * 100:F2}%  ({report.Files} files)"
            );
        }

        Console.WriteLine();
        Console.WriteLine("with symbols supplied:");
        Console.WriteLine(with.Render());
        OneSided(without, with);
        Console.WriteLine();
    }

    return 0;
}

static void Row(string label, FidelityReport report) =>
    Console.WriteLine(
        $"  {label,-34} {report.LineFidelity * 100,7:F2} % {report.FileFidelity * 100,7:F2} %   "
        + $"({report.IdenticalLines.ToString(CultureInfo.InvariantCulture)}/{report.Lines.ToString(CultureInfo.InvariantCulture)})"
    );

// The divergences one symbol set has and the other does not, keyed by what they say rather than by
// where they are: supplying symbols changes how many lines a file has, so a line number is not a
// stable identity across the two runs.
static void OneSided(FidelityReport without, FidelityReport with) {
    var bare = without.Divergences.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
    var defined = with.Divergences.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
    var onlyWith = with.Divergences.Where(divergence => !bare.Contains(KeyOf(divergence))).ToArray();
    var onlyWithout = without.Divergences.Where(divergence => !defined.Contains(KeyOf(divergence))).ToArray();

    Console.WriteLine(
        $"⚠ divergences under one symbol set only: {onlyWith.Length.ToString(CultureInfo.InvariantCulture)} with, "
        + $"{onlyWithout.Length.ToString(CultureInfo.InvariantCulture)} without"
    );

    Sample("  only with symbols", onlyWith);
    Sample("  only without symbols", onlyWithout);
}

static string KeyOf(Divergence divergence) =>
    divergence.File + "\u0000" + divergence.Expected.Trim() + "\u0000" + divergence.Actual.Trim();

static void Sample(string label, IReadOnlyList<Divergence> entries) {
    if (entries.Count == 0) {
        return;
    }

    Console.WriteLine(label + ":");
    foreach (var group in entries.GroupBy(static entry => entry.Class, StringComparer.Ordinal)
                 .OrderByDescending(static group => group.Count())
                 .Take(6)) {
        Console.WriteLine(
            $"    {group.Count().ToString(CultureInfo.InvariantCulture),5}  {group.Key}  ({group.First().File})"
        );
    }
}

// `tree <dir>`: run the oracle and Skala over every .cs file of a tree and report both against the
// tree as committed. Tens of minutes on a large repository, and a developer action like `oracle`.
static int TreeFidelity(string directory, int count) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine("jb is not installed.");
        return 2;
    }

    var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
        .Where(static path => !path.Contains("/obj/", StringComparison.Ordinal)
            && !path.Contains("/bin/", StringComparison.Ordinal)
            && !path.Contains("/.claude/", StringComparison.Ordinal)
        )
        .Select(path => new CorpusFile("tree", Path.GetRelativePath(directory, path).Replace('\\', '/'), path))
        .OrderBy(static file => CorpusSample.KeyOf(CorpusSample.Seed, file.RelativePath))
        .ThenBy(static file => file.RelativePath, StringComparer.Ordinal)
        .Take(count)
        .ToArray();

    Console.WriteLine($"{files.Length.ToString(CultureInfo.InvariantCulture)} files under {directory}");

    var runner = new OracleRunner();
    var config = Path.Combine(directory, ".editorconfig");
    if (!File.Exists(config)) {
        config = Path.Combine(Corpus.RepositoryRoot, ".editorconfig");
    }

    var against = new List<(string File, string Expected, string Actual)>(files.Length);
    var oracleMoved = 0;
    var skalaMoved = 0;
    const int batch = 120;
    for (var start = 0; start < files.Length; start += batch) {
        var slice = files.Skip(start).Take(batch).ToArray();
        var results = runner.Format(slice, config);
        foreach (var file in slice) {
            if (!results.TryGetValue(file.Path, out var expected)) {
                continue;
            }

            var text = CSharpFormatter.Read(file.Path);
            var original = text.ToString();
            var actual = CSharpFormatter.Format(file.Path, text, Resolve(file.Path)).Formatted;
            against.Add((file.ToString(), expected, actual));
            if (!string.Equals(
                    TextNormalisation.Normalise(expected),
                    TextNormalisation.Normalise(original),
                    StringComparison.Ordinal
                )) {
                oracleMoved++;
            }

            if (!string.Equals(
                    TextNormalisation.Normalise(actual),
                    TextNormalisation.Normalise(original),
                    StringComparison.Ordinal
                )) {
                skalaMoved++;
            }
        }

        Console.WriteLine(
            $"  {Math.Min(start + batch, files.Length).ToString(CultureInfo.InvariantCulture)}/{files.Length.ToString(CultureInfo.InvariantCulture)}"
        );
    }

    Console.WriteLine();
    Console.WriteLine(
        $"files the oracle would move: {oracleMoved.ToString(CultureInfo.InvariantCulture)}"
        + $"; files Skala would move: {skalaMoved.ToString(CultureInfo.InvariantCulture)}"
    );

    Console.WriteLine();
    Console.WriteLine("Skala against the oracle, over the whole tree:");
    Console.WriteLine(Fidelity.Compare(against).Render(10));
    return 0;
}

// `dump <set> <dir>`: write Skala's output and the oracle's side by side, so that a divergence
// class named in the report can be read as a diff rather than as two sample lines. A developer
// action like `fidelity`, never part of a test run.
static int Dump(string set, string directory, bool defined) {
    Directory.CreateDirectory(directory);
    var symbols = defined ? Symbols() : [];
    foreach (var file in Corpus.Files(set).Where(static file => file.HasFixture)) {
        var name = file.RelativePath.Replace('/', '_');
        var text = CSharpFormatter.Read(file.Path);
        var result = CSharpFormatter.Format(file.Path, text, Resolve(file.Path), null, symbols);
        File.WriteAllText(Path.Combine(directory, name + ".skala"), TextNormalisation.Normalise(result.Formatted));
        File.WriteAllText(
            Path.Combine(directory, name + ".oracle"),
            TextNormalisation.Normalise(OracleFixture.Read(file))
        );
    }

    return 0;
}

// The differential number per alternative configuration, which is what the preservation ratchet in
// Testing/corpus/fidelity.json is set from.
static int Variants(string[] sets) {
    foreach (var set in sets) {
        foreach (var group in CorpusVariants.Pairs(set)
                     .GroupBy(static pair => pair.Variant, static pair => pair.File)) {
            var results = new List<(string File, string Expected, string Actual)>();
            foreach (var file in group) {
                if (!group.Key.HasFixture(file)) {
                    continue;
                }

                var text = CSharpFormatter.Read(file.Path);
                var options = Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(
                    file.Path,
                    group.Key.Overrides
                ).Options;
                var result = CSharpFormatter.Format(file.Path, text, options);
                results.Add((file.ToString(), OracleFixture.Read(file, group.Key), result.Formatted));
            }

            if (results.Count == 0) {
                continue;
            }

            var report = Fidelity.Compare(results);
            Console.WriteLine($"── {set} [{group.Key.Name}] ──");
            Console.WriteLine(report.Render(6));

            if (Environment.GetEnvironmentVariable("SKALA_VARIANT_DIFF") is { Length: > 0 } directory) {
                Directory.CreateDirectory(directory);
                foreach (var (file, expected, actual) in results) {
                    var name = file.Replace('/', '_') + "." + group.Key.Name;
                    File.WriteAllText(Path.Combine(directory, name + ".oracle"), expected);
                    File.WriteAllText(Path.Combine(directory, name + ".skala"), actual);
                }
            }
        }
    }

    return 0;
}

// `probe`: for every option the formatter reads, which corpus files can tell its values apart.
// The registry's `oracle` glob has to name one of them or the option cannot claim Tier A, and
// finding that by hand across 180 keys is how a tier matrix becomes decoration.
static int Probe() {
    var files = Corpus.Files(Corpus.Constructs);
    foreach (var id in PhaseOneOptions.Implemented) {
        var info = Rikarin.Skala.Options.OptionRegistry.Get(id);
        var values = LegalValues(info).ToArray();
        var matches = new List<string>();

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            var ok = true;
            foreach (var value in values) {
                var builder = new Rikarin.Skala.Options.FormattingOptionsBuilder();
                if (!builder.TrySet(id, value, out _)) {
                    ok = false;
                    break;
                }

                outputs.Add(CSharpFormatter.Format(file.Path, text, new PhaseOneOptions(builder.Build())).Formatted);
            }

            if (ok && outputs.Count > 1) {
                matches.Add("constructs/" + file.RelativePath);
            }
        }

        Console.WriteLine($"{info.Key}\t{(matches.Count == 0 ? "NONE" : string.Join(" ", matches.Take(4)))}");
    }

    return 0;
}

static IEnumerable<string> LegalValues(Rikarin.Skala.Options.OptionInfo info) {
    switch (info.Kind) {
        case Rikarin.Skala.Options.OptionValueKind.Bool:
            yield return "true";
            yield return "false";
            break;

        case Rikarin.Skala.Options.OptionValueKind.Enum:
            foreach (var value in Rikarin.Skala.Options.OptionEnums.ValuesOf(info.EnumName!)) {
                yield return value;
            }

            break;

        case Rikarin.Skala.Options.OptionValueKind.Int:
            var current = int.TryParse(
                info.Default,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number
            )
                    ? number
                    : 0;
            yield return current.ToString(CultureInfo.InvariantCulture);
            yield return (current == 0 ? 3 : current == 1 ? 2 : 0).ToString(CultureInfo.InvariantCulture);
            break;

        default:
            yield return info.Default ?? string.Empty;
            yield return info.Default is null or "" ? "x" : info.Default + "x";
            break;
    }
}

// ⚠ Read out of a real binary log of the same scratch project OracleRunner builds, not typed: the
// measurement and the binlog loader then test each other (PreprocessorFidelity). Memoised, because
// it costs a `dotnet build`.
static IReadOnlyList<string> Symbols() {
    if (SymbolCache.Value is null) {
        try {
            SymbolCache.Value = PreprocessorFidelity.OracleSymbols(Console.Error);
        } catch (Exception exception) when (exception is IOException or InvalidOperationException) {
            Console.Error.WriteLine("the symbol probe failed; falling back to DEBUG;TRACE: " + exception.Message);
            SymbolCache.Value = ["DEBUG", "TRACE"];
        }
    }

    return SymbolCache.Value;
}

static Rikarin.Skala.Options.FormattingOptions Resolve(string path) =>
    Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(path).Options;

/// <summary>The memoised symbol set behind <c>Symbols()</c>.</summary>
/// <remarks>
///     ⚠ A holder type rather than a top-level local, because a top-level local is a local of the
///     generated <c>Main</c> and a static local function may not capture one.
/// </remarks>
static class SymbolCache {
    public static IReadOnlyList<string>? Value { get; set; }
}
