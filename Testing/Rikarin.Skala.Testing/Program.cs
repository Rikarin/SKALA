using System.Globalization;
using Rikarin.Skala.Formatting;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Testing;

// The harness's own entry point. ⚠ Two commands, both deliberate developer-machine actions:
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
//
// It is not the `skala` tool and is never packaged.

if (args.Length == 0) {
    Console.Error.WriteLine("usage: oracle [set…] | fidelity [set…] | dump <set> <dir>");
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

    Console.WriteLine($"{results.Count.ToString(CultureInfo.InvariantCulture)}/{files.Length.ToString(CultureInfo.InvariantCulture)} files answered.");
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

static int Report(string[] sets) {
    foreach (var set in sets) {
        var files = Corpus.Files(set).Where(static file => file.HasFixture).ToArray();
        if (files.Length == 0) {
            continue;
        }

        var results = new List<(string File, string Expected, string Actual)>(files.Length);
        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var result = CSharpFormatter.Format(file.Path, text, Resolve(file.Path));
            results.Add((file.ToString(), OracleFixture.Read(file), result.Formatted));
        }

        Console.WriteLine($"── {set} ──────────────────────────────────────────────────────────");
        Console.WriteLine(Fidelity.Compare(results).Render());

        foreach (var origin in results.GroupBy(static r => r.File.Split('/')[1], StringComparer.Ordinal).OrderBy(
            static g => g.Key,
            StringComparer.Ordinal
        )) {
            var report = Fidelity.Compare(origin);
            Console.WriteLine(
                $"  {origin.Key,-14} line {report.LineFidelity * 100:F2}%  file {report.FileFidelity * 100:F2}%  ({report.Files} files)"
            );
        }

        Console.WriteLine();
    }

    return 0;
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
        foreach (var group in CorpusVariants.Pairs(set).GroupBy(static pair => pair.Variant, static pair => pair.File)) {
            var results = new List<(string File, string Expected, string Actual)>();
            foreach (var file in group) {
                if (!group.Key.HasFixture(file)) {
                    continue;
                }

                var text = CSharpFormatter.Read(file.Path);
                var options = Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(file.Path, group.Key.Overrides).Options;
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
            var current = int.TryParse(info.Default, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
            yield return current.ToString(CultureInfo.InvariantCulture);
            yield return (current == 0 ? 3 : current == 1 ? 2 : 0).ToString(CultureInfo.InvariantCulture);
            break;

        default:
            yield return info.Default ?? string.Empty;
            yield return info.Default is null or "" ? "x" : info.Default + "x";
            break;
    }
}

static Rikarin.Skala.Options.FormattingOptions Resolve(string path) =>
    Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(path).Options;
