using Rikarin.Skala.Release.Surfaces;

namespace Rikarin.Skala.Release;

/// <summary>What the release job was asked to measure.</summary>
public sealed record ReleaseRequest {
    public required string CandidateRoot { get; init; }

    public required SkalaTool CandidateTool { get; init; }

    /// <summary>A checkout of the previous release. Null on the first one.</summary>
    public string? BaselineRoot { get; init; }

    /// <summary>The previous release's built tool. Null on the first one.</summary>
    public SkalaTool? BaselineTool { get; init; }

    /// <summary>The version the previous release published. Null on the first one.</summary>
    public SemanticVersion? BaselineVersion { get; init; }

    public required string WorkRoot { get; init; }

    public string CorpusRoot { get; init; } = "";

    /// <summary>Commits on <c>master</c> since the baseline tag. Drives the pre-release counter.</summary>
    public int Height { get; init; }

    /// <summary>The commit this release would be cut from, for the record.</summary>
    public string Commit { get; init; } = "";

    /// <summary>
    ///     A <c>master</c> build between releases rather than a release. ⚠ Never tags and never
    ///     publishes; it exists so the number and the notes are known before anyone decides to cut one.
    /// </summary>
    public bool DryRun { get; init; } = true;
}

/// <summary>The measurement, the number it implies, and the evidence for both.</summary>
public sealed record ReleaseVerdict(
    SemanticVersion? Previous,
    SemanticVersion Declared,
    SemanticVersion Next,
    BumpKind Bump,
    bool AnySurfaceMeasured,
    IReadOnlyList<DetectorResult> Surfaces,
    OutputMeasurement? Output,
    string BaselineFingerprint,
    string CandidateFingerprint
) {
    public string Tag => "v" + Next;
}

/// <summary>
///     docs/plan/18 as a program: measure every surface, take the highest verdict, apply it.
/// </summary>
public static class ReleasePlan {
    public static ReleaseVerdict Measure(ReleaseRequest request) {
        Directory.CreateDirectory(request.WorkRoot);

        var corpus = string.IsNullOrEmpty(request.CorpusRoot)
            ? Path.Combine(request.CandidateRoot, "Testing", "corpus")
            : request.CorpusRoot;

        var (output, measurement) = OutputSurface.Run(
            request.BaselineTool,
            request.CandidateTool,
            request.BaselineRoot,
            corpus,
            request.CandidateRoot,
            Path.Combine(request.WorkRoot, "corpus")
        );

        var surfaces = new List<DetectorResult> {
            output,
            RuleSurface.Run(request.BaselineRoot, request.CandidateRoot),
            ExitCodeSurface.Run(
                request.BaselineTool,
                request.CandidateTool,
                request.BaselineRoot,
                request.CandidateRoot,
                request.WorkRoot
            ),
            SarifSurface.Run(request.BaselineTool, request.CandidateTool, request.WorkRoot),
            OptionSurface.Run(request.BaselineRoot, request.CandidateRoot)
        };

        // ⚠ The floor is patch and the verdict is the highest contribution, not the sum. An
        // unmeasured surface contributes nothing — see `DetectorResult.Contribution`, and the
        // `AnySurfaceMeasured` flag below, which is what stops "every detector was blind" from
        // rendering as "nothing changed".
        var bump = surfaces
            .Select(static surface => surface.Contribution)
            .Where(static contribution => contribution is not null)
            .Aggregate(BumpKind.Patch, static (highest, contribution) => RuleSurface.Max(highest, contribution!.Value));

        var declared = VersionSources.Declared(request.CandidateRoot);
        var previous = request.BaselineVersion;

        // The number the release moves from: the last published one when there is one, and what the
        // tree declares when there is not.
        var current = previous ?? declared;
        var next = current.Next(bump);

        // ⚠ A dry run on `master` is not a release, so it is stamped as one build of the *pending*
        // pre-release series rather than as the release itself. Height is the commit count since the
        // baseline tag, so the number is reconstructible from the repository alone.
        //
        // ⚠ The counter is the baseline's plus the height, not the height. When the baseline tag is
        // itself a pre-release of the same release — `v2.0.0-alpha.126`, one commit ago — the height
        // is 1 and a bare height would stamp `2.0.0-alpha.1`, which sorts *below* the tag it was
        // measured against. NuGet would then resolve the older package for `--prerelease`, and a
        // version that goes backwards is worse than no version. Found by the mutant run in doc 18
        // § "Proving the detector fires", which produced exactly that.
        if (request.DryRun && request.Height > 0) {
            var continues = previous is { IsPreRelease: true }
                && previous.Major == next.Major
                && previous.Minor == next.Minor
                && previous.Patch == next.Patch;

            next = next.AsPreRelease(
                next.PreReleaseLabel ?? "alpha",
                (continues ? previous!.PreReleaseCounter : 0) + request.Height
            );
        }

        return new ReleaseVerdict(
            previous,
            declared,
            next,
            bump,
            surfaces.Any(static surface => surface.State == DetectorState.Measured),
            surfaces,
            measurement,
            request.BaselineTool?.Fingerprint ?? "",
            request.CandidateTool.Fingerprint
        );
    }
}
