using System.Globalization;
using System.Text.Json;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>
///     The shape of the SARIF, measured by producing one from each build.
/// </summary>
/// <remarks>
///     ⚠ ADR-012 freezes the SARIF shape, and doc 09 makes the report the machine-readable contract that
///     baselines, gates, the MCP surface and every CI integration are written against. So the shape is
///     read out of a <b>real report from a real run of each binary</b>, not out of the writer's source:
///     a report model can be refactored without changing a byte of output, and a serializer setting can
///     change every byte without touching the model.
///     <para>
///         ⚠ What is compared is the set of JSON <b>paths</b> and their value kinds, with array indices
///         collapsed and unioned across elements — never the values. The report carries the tool's own
///         version, two timestamps and a fingerprint per finding, all of which differ between any two runs;
///         a value comparison would report "the SARIF changed" on every release and mean nothing.
///     </para>
///     <para>
///         ⚠ The probe input is deliberately misformatted so that <c>SK0001</c> fires. An empty
///         <c>results[]</c> exercises none of <c>locations</c>, <c>partialFingerprints</c> or <c>fixes</c> —
///         which is most of the shape a consumer depends on, and <c>partialFingerprints</c> is what every
///         baseline in every repository is keyed on.
///     </para>
/// </remarks>
public static class SarifSurface {
    public const string Name = "SARIF shape";

    /// <summary>
    ///     Misformatted on purpose: <c>SK0001</c> is syntactic, so it fires under <c>--load=loose</c> on
    ///     any machine, with no SDK, no project and no restore.
    /// </summary>
    const string ProbeSource = "class  C{ async void  M( ){} }\n";

    public static DetectorResult Run(SkalaTool? baseline, SkalaTool candidate, string workRoot) {
        var candidateShape = Shape(candidate, Path.Combine(workRoot, "sarif-candidate"));

        if (baseline is null) {
            return DetectorResult.Unmeasured(
                Name,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no previous release — this one's report has {candidateShape.Count} distinct paths"
                )
            );
        }

        var baselineShape = Shape(baseline, Path.Combine(workRoot, "sarif-baseline"));

        var added = candidateShape.Except(baselineShape, StringComparer.Ordinal)
            .OrderBy(static p => p, StringComparer.Ordinal)
            .ToList();
        var removed = baselineShape.Except(candidateShape, StringComparer.Ordinal)
            .OrderBy(static p => p, StringComparer.Ordinal)
            .ToList();

        var details = new List<string>();
        details.AddRange(removed.Select(static path => $"**removed** `{path}`"));
        details.AddRange(added.Select(static path => $"added `{path}`"));

        // ⚠ Both directions are major. A removed path breaks a reader that depends on it; an added
        // one breaks a reader that validates against a closed schema, and doc 09 publishes the shape
        // rather than a subset of it.
        var bump = details.Count > 0 ? BumpKind.Major : BumpKind.Patch;
        var headline = details.Count == 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"unchanged — {candidateShape.Count} paths, identical to the previous release"
            )
            : string.Create(CultureInfo.InvariantCulture, $"{removed.Count} path(s) removed, {added.Count} added");

        return DetectorResult.Measured(Name, bump, headline, details);
    }

    static SortedSet<string> Shape(SkalaTool tool, string workRoot) {
        if (Directory.Exists(workRoot)) {
            Directory.Delete(workRoot, true);
        }

        Directory.CreateDirectory(workRoot);
        File.WriteAllText(Path.Combine(workRoot, "Probe.cs"), ProbeSource);

        // ⚠ An absolute output path. A bare filename crashes the tool with exit 5 — see doc 18
        // § "What the pipeline found on its first run".
        var report = Path.Combine(workRoot, "report.sarif");
        // ⚠ The probe file by absolute path, never `.`. The tool resolves a bare `.` against the
        // enclosing repository rather than the process's working directory, and the scratch tree
        // lives under `artifacts/`, so `.` walked up and analysed nothing.
        var run = tool.Run(
            workRoot,
            "check",
            "--load=loose",
            "--include-hints",
            "--output",
            report,
            Path.Combine(workRoot, "Probe.cs")
        );

        if (!File.Exists(report)) {
            throw new InvalidOperationException(
                $"'{tool.Path}' wrote no SARIF (exit {run.ExitCode}).\n{run.StandardOutput}\n{run.StandardError}"
            );
        }

        using var document = JsonDocument.Parse(File.ReadAllText(report));
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        Walk(document.RootElement, "$", paths);

        if (paths.Count < 20) {
            throw new InvalidOperationException(
                $"'{tool.Path}' produced a SARIF with only {paths.Count} paths. The probe is supposed to "
                + "produce at least one finding with a location, a fingerprint and a fix; a near-empty "
                + "report would compare equal to any other near-empty report."
            );
        }

        return paths;
    }

    /// <summary>Every path in the document, with array indices collapsed and values discarded.</summary>
    static void Walk(JsonElement element, string path, SortedSet<string> paths) {
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) {
                    Walk(property.Value, path + "." + property.Name, paths);
                }

                // An object with no properties is still a shape: recording it keeps
                // `properties: {}` distinguishable from the property being absent.
                if (!element.EnumerateObject().Any()) {
                    paths.Add(path + " : {}");
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) {
                    Walk(item, path + "[]", paths);
                }

                if (!element.EnumerateArray().Any()) {
                    paths.Add(path + "[] : []");
                }

                break;

            default:
                // The kind travels with the path, so a field that turns from a number into a string
                // is a change even though the path survived. ⚠ `true` and `false` collapse to one
                // kind — they are the same field, and `System.Text.Json` gives them two.
                paths.Add(
                    path
                    + " : "
                    + (element.ValueKind is JsonValueKind.True or JsonValueKind.False
                            ? "boolean"
                            : element.ValueKind.ToString().ToLowerInvariant())
                );

                break;
        }
    }
}
