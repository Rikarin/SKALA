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
//
// It is not the `skala` tool and is never packaged.

if (args.Length == 0) {
    Console.Error.WriteLine("usage: oracle [set…] | fidelity [set…]");
    return 2;
}

var sets = args.Length > 1 ? args[1..] : [Corpus.Constructs, Corpus.Real, Corpus.Pathological];

switch (args[0]) {
    case "oracle":
        return Regenerate(sets);
    case "fidelity":
        return Report(sets);
    default:
        Console.Error.WriteLine($"unknown command '{args[0]}'");
        return 2;
}

static int Regenerate(string[] sets) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine(
            "jb is not installed. `dotnet tool install -g JetBrains.ReSharper.GlobalTools --version 2025.2.6`.");
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

            Console.WriteLine($"  {set}: {Math.Min(start + batch, files.Count).ToString(CultureInfo.InvariantCulture)}/{files.Count.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    Console.WriteLine($"{written.ToString(CultureInfo.InvariantCulture)} fixtures written.");
    return 0;
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

        foreach (var origin in results.GroupBy(static r => r.File.Split('/')[1], StringComparer.Ordinal).OrderBy(static g => g.Key, StringComparer.Ordinal)) {
            var report = Fidelity.Compare(origin);
            Console.WriteLine($"  {origin.Key,-14} line {report.LineFidelity * 100:F2}%  file {report.FileFidelity * 100:F2}%  ({report.Files} files)");
        }

        Console.WriteLine();
    }

    return 0;
}

static Rikarin.Skala.Options.FormattingOptions Resolve(string path) =>
    Rikarin.Skala.Core.Configuration.OptionResolver.Resolve(path).Options;
