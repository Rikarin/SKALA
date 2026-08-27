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
//   dump <set> <dir>  write Skala's output and the oracle's side by side, so a class named in the
//                     report can be read as a diff rather than as two sample lines.
//   variants [set…]   the differential number for each alternative configuration a set is run
//                     under — docs/plan/05's four-way keep_existing_* table.
//   constructs [set]  every divergent line attributed to the construct that owns it, beside how
//                     often that construct occurs — docs/plan/16 § R1's actual question.
//   ask <dir>         run the oracle over a scratch directory, in place. The tool the milestone-3
//                     wrapping rules were established with: an option name does not say what
//                     happens to a 121-column array initializer, and asking does.
//   audit [dir…]      every rule's findings over a tree, grouped by rule, for the
//                     false-positive review docs/plan/16 § R3 makes the shipping bar.
//   sample <tree> <n> <dest>
//                     redraw a corpus sample from a tree, reproducibly: the file is chosen by a
//                     hash of its path rather than by a seeded sequence, so the same commit and
//                     the same filters give the same files on any machine.
//   margin [out]      SK-DIV-0005's constant, swept: eleven right-hand-side shapes at five block
//                     depths under both values of `wrap_before_eq`, one character at a time.
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
    );
    return 2;
}

var sets = args.Length > 1 ? args[1..] : [Corpus.Constructs, Corpus.Real, Corpus.Pathological];

switch (args[0]) {
    case "oracle":
        return Regenerate(sets);
    case "fidelity":
        return Report(sets);
    case "dump":
        return Dump(args[1], args[2]);
    case "variants":
        return Variants(sets);
    case "probe":
        return Probe();
    case "ask":
        return Ask(args[1], args[2..]);
    case "defaults":
        return Defaults(args.Length > 1 ? args[1] : null);
    case "audit":
        // docs/plan/16 § R3's shipping bar, as a list a person can read: every rule's findings over
        // a tree, grouped, so that "zero false positives" is checked rather than claimed.
        Console.WriteLine(RuleAudit.Run(args.Length > 1 ? args[1..] : [Corpus.SetRoot(Corpus.Real)], true));
        return 0;
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
    case "constructs":
        // docs/plan/16 § R1: any construct occurring more than 50 times must be at 100 %. A single
        // fidelity number cannot answer that; this attributes every divergent line to the construct
        // that owns it and puts it beside how often the construct occurs.
        Console.WriteLine(ConstructReport.Render(ConstructReport.Build(args.Length > 1 ? args[1] : Corpus.Real)));
        return 0;
    default:
        Console.Error.WriteLine($"unknown command '{args[0]}'");
        return 2;
}

// `ask <dir> [key=value…]`: run the oracle over a scratch directory of .cs files, in place, under
// the repository's .editorconfig plus any overrides.
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
    foreach (var entry in overrides) {
        if (entry.StartsWith("--config=", StringComparison.Ordinal)) {
            config = Path.GetFullPath(entry["--config=".Length..]);
            continue;
        }

        var equals = entry.IndexOf('=', StringComparison.Ordinal);
        if (equals > 0) {
            pairs.Add(new KeyValuePair<string, string>(entry[..equals].Trim(), entry[(equals + 1)..].Trim()));
        }
    }

    var results = new OracleRunner().Format(files, config, pairs);
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

static int Regenerate(string[] sets) {
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
        var files = Corpus.Files(set);
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
    Console.WriteLine($"{written.ToString(CultureInfo.InvariantCulture)} fixtures written.");
    return 0;
}

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
        Row("no symbols", without);
        Row("with symbols", with);
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
        $"  {label,-16} {report.LineFidelity * 100,7:F2} % {report.FileFidelity * 100,7:F2} %   "
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

// `dump <set> <dir>`: write Skala's output and the oracle's side by side, so that a divergence
// class named in the report can be read as a diff rather than as two sample lines. A developer
// action like `fidelity`, never part of a test run.
static int Dump(string set, string directory) {
    Directory.CreateDirectory(directory);
    foreach (var file in Corpus.Files(set).Where(static file => file.HasFixture)) {
        var name = file.RelativePath.Replace('/', '_');
        var text = CSharpFormatter.Read(file.Path);
        var result = CSharpFormatter.Format(file.Path, text, Resolve(file.Path));
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
/// ⚠ A holder type rather than a top-level local, because a top-level local is a local of the
/// generated <c>Main</c> and a static local function may not capture one.
/// </remarks>
static class SymbolCache {
    public static IReadOnlyList<string>? Value { get; set; }
}
