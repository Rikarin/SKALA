using System.Globalization;
using Rikarin.Skala.Conformance.Sweep;
using Rikarin.Skala.Testing;

// The key-flip conformance sweep's entry point. ⚠ Every command here shells out to
// `jb cleanupcode` and takes minutes; none of them is a test, and none runs on a commit.
//
//   sweep [--family=space,wrap] [--out=<path>]
//                     every option, at every legal value, Skala against the oracle under the same
//                     configuration. Writes the committed result table.
//   defaults [--family=…] [--apply]
//                     the same machinery under a bare configuration, which is the defaults
//                     measurement: the value that reproduces `jb cleanupcode` with nothing but
//                     `root = true` is ReSharper's default. `--apply` writes the verified ones back
//                     into options.json.
//   plan [--family=…] what the sweep would ask about and what it would cost, without running it.
//   verify <key>      one option, unbatched, with both engines' output at every value printed in
//                     full. ⚠ How a row in the table is checked before anything is demoted on the
//                     strength of it.
//   nightly [--family=…] [--apply]
//                     both passes in one process: the export-base sweep, then the bare-base defaults
//                     pass cross-checked against it. ⚠ The shape the nightly job wants — the
//                     cross-check that separates a weak fixture from a masked key needs both passes'
//                     view, and running them separately makes the first write a sidecar for the
//                     second to read back.

if (args.Length == 0) {
    Console.Error.WriteLine(
        "usage: sweep | defaults | nightly [--family=…] [--out=…] [--apply] | plan [--family=…] | verify <key>"
    );
    return 2;
}

var families = Flag("--family") is { Length: > 0 } list
    ? list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : [];

var plan = SweepPlan.Build(families);

switch (args[0]) {
    case "plan":
        return Plan();
    case "sweep":
        return Sweep();
    case "defaults":
        return Defaults(null);
    case "nightly":
        return Nightly();
    case "verify":
        if (args.Length < 2) {
            Console.Error.WriteLine("usage: verify <key>");
            return 2;
        }

        return SweepVerify.Run(
            SweepPlan.Build([]),
            args[1],
            Path.Combine(Corpus.RepositoryRoot, ".editorconfig"),
            Console.Out
        );
    default:
        Console.Error.WriteLine("unknown command: " + args[0]);
        return 2;
}

int Plan() {
    Console.WriteLine(
        $"{Count(plan.Candidates.Count)} options, "
        + $"{Count(plan.Candidates.Sum(static candidate => candidate.Values.Count))} configurations, "
        + $"{Count(plan.Candidates.Count == 0 ? 0 : plan.Candidates.Max(static candidate => candidate.Values.Count))} rounds"
    );

    foreach (var group in plan.Excluded
                 .GroupBy(static exclusion => exclusion.Reason, StringComparer.Ordinal)
                 .OrderByDescending(static group => group.Count())) {
        Console.WriteLine($"  not swept: {Count(group.Count())}  {group.Key}");
    }

    foreach (var candidate in plan.Candidates.OrderBy(static c => c.Key, StringComparer.Ordinal)) {
        Console.WriteLine(
            $"  {candidate.Key.PadRight(58)}{candidate.Info.Tier.ToString().PadRight(3)}"
            + $"{Count(candidate.Values.Count).PadLeft(2)}  {candidate.Fixture}"
        );
    }

    return 0;
}

int Sweep() => Measure(out _);

int Measure(out SweepRun? measured) {
    measured = null;
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine(
            "jb (JetBrains.ReSharper.GlobalTools) is not installed. The sweep is a nightly job and a "
            + "developer-machine dependency (ADR-011); the committed table is what the fast path reads."
        );
        return 3;
    }

    var config = Path.Combine(Corpus.RepositoryRoot, ".editorconfig");
    var run = new KeyFlipSweep(new OracleRunner(), config, Console.Out).Run(plan);
    var text = SweepReport.Render(run, families);

    var output = Flag("--out")
        ?? Path.Combine(
            Corpus.RepositoryRoot,
            "Testing",
            "Rikarin.Skala.Conformance.Sweep",
            "conformance-sweep.md"
        );

    File.WriteAllText(output, text);

    // ⚠ The sidecar is what the defaults pass reads to tell "this fixture is too weak" from
    // "ReSharper's own defaults mask this option". The two passes run under different base
    // configurations, so the export-base run writes down what the bare one cannot observe.
    var archive = Path.ChangeExtension(output, ".json");
    SweepArchive.Write(archive, run);

    Console.WriteLine();
    Console.WriteLine(Summary(run));
    Console.WriteLine("written: " + output);
    Console.WriteLine("written: " + archive);
    measured = run;
    return 0;
}

int Nightly() {
    var status = Measure(out var run);
    if (status != 0 || run is null) {
        return status;
    }

    Console.WriteLine();
    return Defaults(
        [.. run.Options.Where(static option => option.OracleDistinct > 1).Select(static option => option.Key)]
    );
}

int Defaults(IReadOnlyCollection<string>? inProcess) {
    if (OracleRunner.FindExecutableOrNull() is null) {
        Console.Error.WriteLine("jb (JetBrains.ReSharper.GlobalTools) is not installed.");
        return 3;
    }

    var probed = new DefaultsPass(new OracleRunner(), Console.Out).Run(plan);

    var archive = Path.Combine(
        Corpus.RepositoryRoot,
        "Testing",
        "Rikarin.Skala.Conformance.Sweep",
        "conformance-sweep.json"
    );
    // ⚠ In-process first. `nightly` hands the sweep's own view straight over; a bare `defaults` run
    // falls back to the last sweep's sidecar, which may be from another day.
    var observable = inProcess ?? SweepArchive.ReadObservable(archive);
    if (observable is not null) {
        probed = DefaultsPass.CrossCheck(probed, observable);
        Console.WriteLine(
            $"cross-checked against {Count(observable.Count)} options the export-base sweep watched the oracle distinguish"
        );
    } else {
        // ⚠ Said rather than silently skipped. Without the cross-check, every `Insensitive` verdict
        // reads as "this fixture is too weak", and some of them are "bare defaults mask this key" —
        // which is a fact about the configuration and not a reason to replace a fixture.
        Console.WriteLine(
            "⚠ no " + archive + ": `Insensitive` cannot be separated from `masked by bare defaults`. Run `sweep` first."
        );
    }

    Console.WriteLine();
    Console.WriteLine(DefaultsPass.Render(probed));

    var registry = Path.Combine(Corpus.RepositoryRoot, "Core", "Rikarin.Skala.Options", "options.json");
    var patch = RegistryPatch.Plan(registry, probed);
    Console.WriteLine(RegistryPatch.Render(patch));

    if (args.Contains("--apply", StringComparer.Ordinal)) {
        RegistryPatch.Apply(registry, patch);
        Console.WriteLine($"applied {Count(patch.Count)} changes to {registry}");
    } else {
        Console.WriteLine("Re-run with --apply to write options.json.");
    }

    return 0;
}

string Summary(SweepRun run) {
    var lines = Enum.GetValues<SweepOutcome>()
        .Select(outcome => $"{outcome.ToString().ToUpperInvariant()}: {Count(run.Options.Count(o => o.Outcome == outcome))}"
        );
    return string.Join("   ", lines)
        + $"\noracle wall clock {run.OracleWallClock.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s"
        + $" over {Count(run.OracleInvocations)} invocations"
        + $" = {(run.Options.Count == 0 ? 0 : run.OracleWallClock.TotalSeconds / run.Options.Count).ToString("F2", CultureInfo.InvariantCulture)} s per option";
}

string? Flag(string name) =>
    args.FirstOrDefault(argument => argument.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..];

static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
