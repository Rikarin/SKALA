using System.Globalization;
using System.Text.Json;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     <c>check --output-unsuppressed</c>: the narrower SARIF the code-scanning upload gets (#332).
/// </summary>
/// <remarks>
///     ⚠ <b>GitHub code scanning does not consume the SARIF <c>suppressions</c> property.</b> It is
///     absent from every table in GitHub's SARIF support documentation, and the measurement agreed: all
///     five open <c>SK6034</c> alerts on this repository were findings <c>.skala/baseline.sarif</c>
///     accepts, matched by (rule, path, line), and in one run 1 145 of 1 163 results were baselined —
///     so the alert list was 98 % noise and the 18 findings the gate failed on were invisible in it.
///     <para>
///         ⚠ <b>The full log does not change, and that is half of what these assert.</b> It is correct
///         SARIF, it is what <c>report</c>, <c>trend</c> and <c>baseline</c> read, and the workflow
///         renders the PR comment and keeps the artefact from it. Only the upload gets the narrow view.
///     </para>
///     <para>
///         ⚠ Driven through the real binary rather than through <c>SarifWriter</c>. The writer is pinned
///         by <c>SarifSuppressionTests</c>; what only a process can see is whether the option is bound,
///         whether both files are written from the <em>same</em> run, and whether asking for the second
///         one damaged the first.
///     </para>
/// </remarks>
public sealed class CodeScanningSarifTests : IDisposable {
    readonly CrossPlatformScratch scratch = new("skala-upload-");

    public void Dispose() => scratch.Dispose();

    /// <summary>
    ///     ⚠ (rule, <b>path</b>, line), and the path is what this test got wrong first. Every finding
    ///     here sits on line 1, so a key of (rule, line) made the two files compare equal and the
    ///     difference the test exists to see vanished into a set operation.
    /// </summary>
    static string[] Fingerprints(string path) {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return [
            .. document.RootElement
                .GetProperty("runs")[0]
                .GetProperty("results")
                .EnumerateArray()
                .Select(static result => {
                        var location = result.GetProperty("locations")[0].GetProperty("physicalLocation");
                        return (result.GetProperty("ruleId").GetString() ?? string.Empty)
                            + "@"
                            + location.GetProperty("artifactLocation").GetProperty("uri").GetString()
                            + ":"
                            + location.GetProperty("region")
                                .GetProperty("startLine")
                                .GetInt32()
                                .ToString(CultureInfo.InvariantCulture);
                    }
                )
        ];
    }

    [Fact]
    public void TheUploadedSarif_OmitsWhatTheBaselineAccepts_AndTheKeptOneDoesNot() {
        scratch.InitialiseGit();
        scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");

        // Two files, so the baseline can accept one of them and the other can stay open. The
        // contents are irrelevant beyond firing something; what matters is the bucket each lands in.
        scratch.WriteText(Path.Combine("src", "Accepted.cs"), "class C{void M(){M();}}\n");

        var baseline = scratch.Run("baseline", "create", "--load=loose", "--apply", ".");
        Assert.True(
            File.Exists(Path.Combine(scratch.Root, ".skala", "baseline.sarif")),
            $"baseline create wrote nothing. exit={baseline.ExitCode}\n{baseline.StandardOutput}\n{baseline.StandardError}"
        );

        // Added after the baseline, so its findings are the only ones outside it.
        scratch.WriteText(Path.Combine("src", "New.cs"), "class D{void M(){M();}}\n");

        var full = Path.Combine(scratch.Root, "report.sarif");
        var upload = Path.Combine(scratch.Root, "code-scanning.sarif");
        var run = scratch.Run(
            "check",
            "--load=loose",
            "--no-cache",
            "--baseline",
            Path.Combine(".skala", "baseline.sarif"),
            "--output",
            full,
            "--output-unsuppressed",
            upload,
            "."
        );

        Assert.True(
            File.Exists(full) && File.Exists(upload),
            $"check wrote {(File.Exists(full) ? "" : "no report.sarif ")}{(File.Exists(upload) ? "" : "no code-scanning.sarif")}. exit={run.ExitCode}\n{run.StandardOutput}\n{run.StandardError}"
        );

        var kept = Fingerprints(full);
        var uploaded = Fingerprints(upload);

        // Anti-vacuity: the baseline really did accept something, so neither side of the comparison
        // below is empty by accident.
        Assert.NotEmpty(kept);
        var accepted = kept.Except(uploaded, StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(accepted);

        // The direction the issue is about: an accepted finding is absent from the upload …
        Assert.All(accepted, entry => Assert.DoesNotContain(entry, uploaded, StringComparer.Ordinal));

        // … and present in the kept report, which is what `report`, `trend` and `baseline` read. A
        // writer that dropped suppressed results everywhere would pass the assertion above and fail
        // this one.
        Assert.All(accepted, entry => Assert.Contains(entry, kept, StringComparer.Ordinal));

        // Nothing is invented: the upload is a subset.
        Assert.All(uploaded, entry => Assert.Contains(entry, kept, StringComparer.Ordinal));

        // And the suppressed results are gone by the property that names them, not by rule id.
        using var document = JsonDocument.Parse(File.ReadAllText(upload));
        Assert.All(
            document.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray(),
            static result => Assert.False(result.TryGetProperty("suppressions", out _))
        );

        using var keptDocument = JsonDocument.Parse(File.ReadAllText(full));
        Assert.Contains(
            keptDocument.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray(),
            static result => result.TryGetProperty("suppressions", out _)
        );
    }

    /// <summary>
    ///     ⚠ Off unless asked for. The second file is an opt-in for a consumer that needs it; a run
    ///     that does not name one must not start littering a tree with a file nobody reads.
    /// </summary>
    [Fact]
    public void NoSecondFile_IsWrittenWithoutTheOption() {
        scratch.InitialiseGit();
        scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");
        scratch.WriteText(Path.Combine("src", "A.cs"), "class C{void M(){M();}}\n");

        var full = Path.Combine(scratch.Root, "report.sarif");
        scratch.Run("check", "--load=loose", "--no-cache", "--output", full, ".");

        Assert.True(File.Exists(full));
        Assert.Equal([full], Directory.GetFiles(scratch.Root, "*.sarif"));
    }
}
