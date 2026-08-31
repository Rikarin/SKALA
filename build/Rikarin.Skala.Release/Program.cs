using Rikarin.Skala.Release;
using System.Globalization;
using System.Text.Json;

// The measured-version tool. docs/plan/18-versioning-and-release.md.
//
//   plan  measure every compatibility surface against the previous release, print the version that
//         measurement implies, and write the release notes it implies.
//
// ⚠ It computes and writes. It never tags, never pushes and never publishes: the tag is the
// workflow's to create and the publish is a person's to arm. See .github/workflows/release.yml.

if (args.Length == 0 || args[0] is "-h" or "--help") {
    Console.WriteLine(
        """
        usage: skala-release plan [options]

          --candidate <dir>        the tree being released. Default: the repository this was built from.
          --candidate-tool <path>  its built `skala.dll` or published `skala`.
          --baseline <dir>         a checkout of the previous release. Omit for the first release.
          --baseline-tool <path>   the previous release's built tool. Omit for the first release.
          --baseline-version <v>   the version the previous release published. Omit for the first release.
          --corpus <dir>           default: <candidate>/Testing/corpus
          --height <n>             commits since the baseline tag; the pre-release counter.
          --commit <sha>           recorded in the notes.
          --release                cut a release rather than a `master` dry run: no `-alpha.<height>`.
          --work <dir>             scratch. Default: <candidate>/artifacts/release/work
          --out <dir>              where version.json, release-notes.md and summary.md go.
                                   Default: <candidate>/artifacts/release
        """
    );

    return args.Length == 0 ? 2 : 0;
}

if (args[0] != "plan") {
    Console.Error.WriteLine($"skala-release: unknown command '{args[0]}'.");
    return 3;
}

string? Option(string name) {
    var index = Array.IndexOf(args, "--" + name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

bool Flag(string name) => Array.IndexOf(args, "--" + name) >= 0;

var candidateRoot = Path.GetFullPath(Option("candidate") ?? Rikarin.Skala.Testing.Corpus.RepositoryRoot);
var candidateTool = Option("candidate-tool")
    ?? Path.Combine(candidateRoot, "Tools", "Rikarin.Skala.Cli", "bin", "Release", "net10.0", "skala.dll");

var baselineRoot = Option("baseline") is { Length: > 0 } baseline ? Path.GetFullPath(baseline) : null;
var baselineToolPath = Option("baseline-tool");

// ⚠ A baseline tree without a baseline tool is refused rather than half-measured. Three of the five
// detectors read files and two run binaries; a run that quietly skipped the two would report a
// patch on a release that reformats every file, which is the failure doc 18 is built around.
if (baselineRoot is not null && string.IsNullOrEmpty(baselineToolPath)) {
    Console.Error.WriteLine(
        "skala-release: --baseline needs --baseline-tool. The output and SARIF detectors run the "
        + "previous release's binary; without it they cannot report anything but 'unmeasured', and a "
        + "release measured on three of five surfaces is not measured."
    );

    return 3;
}

var outputDirectory = Path.GetFullPath(Option("out") ?? Path.Combine(candidateRoot, "artifacts", "release"));
var workRoot = Path.GetFullPath(Option("work") ?? Path.Combine(outputDirectory, "work"));
Directory.CreateDirectory(outputDirectory);

var request = new ReleaseRequest {
    CandidateRoot = candidateRoot,
    CandidateTool = SkalaTool.At(candidateTool),
    BaselineRoot = baselineRoot,
    BaselineTool = baselineToolPath is { Length: > 0 } ? SkalaTool.At(baselineToolPath) : null,
    BaselineVersion = SemanticVersion.TryParse(Option("baseline-version"), out var previous) ? previous : null,
    CorpusRoot = Option("corpus") ?? "",
    WorkRoot = workRoot,
    Height = int.TryParse(Option("height"), NumberStyles.None, CultureInfo.InvariantCulture, out var height)
        ? height
        : 0,
    Commit = Option("commit") ?? "",
    DryRun = !Flag("release")
};

if (request.BaselineRoot is not null && request.BaselineVersion is null) {
    Console.Error.WriteLine("skala-release: --baseline needs --baseline-version.");
    return 3;
}

ReleaseVerdict verdict;
try {
    verdict = ReleasePlan.Measure(request);
} catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException) {
    // ⚠ Reported, never swallowed into a patch. A detector that cannot run is the release stopping,
    // not the release being small.
    Console.Error.WriteLine("skala-release: " + exception.Message);
    return 1;
}

var date = DateTimeOffset.UtcNow;
var notes = ReleaseNotes.Render(verdict, request, date);
var changelog = ReleaseNotes.Changelog(verdict, date);

File.WriteAllText(Path.Combine(outputDirectory, "release-notes.md"), notes);
File.WriteAllText(Path.Combine(outputDirectory, "changelog-entry.md"), changelog);
File.WriteAllText(
    Path.Combine(outputDirectory, "version.json"),
    JsonSerializer.Serialize(
        new {
            version = verdict.Next.ToString(),
            tag = verdict.Tag,
            previous = verdict.Previous?.ToString(),
            declared = verdict.Declared.ToString(),
            bump = verdict.Bump.ToString().ToLowerInvariant(),
            release = !request.DryRun,
            commit = request.Commit,
            baselineTool = verdict.BaselineFingerprint,
            candidateTool = verdict.CandidateFingerprint,
            surfaces = verdict.Surfaces.Select(static surface => new {
                    surface = surface.Surface,
                    state = surface.State.ToString().ToLowerInvariant(),
                    bump = surface.State == DetectorState.Measured ? surface.Bump.ToString().ToLowerInvariant() : null,
                    headline = surface.Headline,
                    details = surface.Details
                }
            ),
            output = verdict.Output is null
                ? null
                : new {
                    comparable = verdict.Output.Comparable,
                    addedToCorpus = verdict.Output.AddedToCorpus,
                    baselineRefused = verdict.Output.BaselineRefused,
                    candidateRefused = verdict.Output.CandidateRefused,
                    changedFiles = verdict.Output.ChangedFiles,
                    changedLines = verdict.Output.ChangedLines,
                    classes = verdict.Output.Classes.Select(static entry => new {
                            entry.Class, entry.Lines, entry.Files, entry.Example
                        }
                    )
                }
        },
        new JsonSerializerOptions { WriteIndented = true }
    )
);

Console.WriteLine(notes);
Console.WriteLine();
Console.WriteLine($"version.json, release-notes.md and changelog-entry.md written to {outputDirectory}");
return 0;
