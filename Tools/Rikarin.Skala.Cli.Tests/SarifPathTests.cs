using System.Text.Json;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
/// doc 12 § "Cross-platform", hazard 2: paths in SARIF must be repo-relative with forward slashes.
/// </summary>
/// <remarks>
/// ⚠ <c>RelativePathTests</c> in <c>Rikarin.Skala.Reporting.Tests</c> already pins
/// <c>SarifWriter.Relative</c> as a function, and it is not enough. The function was correct and
/// the report was still absolute for three milestones, because the defect was never in the string
/// arithmetic — it was in what the two arguments were: a repository root the CLI found with its own
/// second copy of <c>FindRepositoryRoot</c> that returned null inside a git worktree, against file
/// paths the loader had produced through a different API. Both halves are outside the function, and
/// only a real run can see them.
/// <para>
/// So this drives <c>skala check</c> over a real git repository, reads the SARIF it wrote, and
/// walks every <c>artifactLocation</c> in the document. What it asserts is what
/// <a href="https://docs.github.com/code-security">code scanning</a> requires and silently drops
/// results for rather than reporting: a URI reference, which a Windows absolute path
/// (<c>C:\src\a.cs</c>) is not, and which a backslash cannot appear in.
/// </para>
/// </remarks>
public sealed class SarifPathTests : IDisposable {
    readonly CrossPlatformScratch _scratch = new("skala-sarif-");

    public void Dispose() => _scratch.Dispose();

    /// <summary>Every <c>artifactLocation.uri</c> anywhere in the document, however deeply nested.</summary>
    static List<string> ArtifactUris(JsonElement element) {
        var uris = new List<string>();
        Walk(element);
        return uris;

        void Walk(JsonElement node) {
            switch (node.ValueKind) {
                case JsonValueKind.Object:
                    foreach (var property in node.EnumerateObject()) {
                        // ⚠ `artifactLocation` specifically, not every "uri" in the document: the
                        // tool driver carries an `informationUri` that is an absolute https URL and
                        // is supposed to be.
                        if (property.NameEquals("artifactLocation")
                            && property.Value.TryGetProperty("uri", out var uri)
                            && uri.ValueKind == JsonValueKind.String) {
                            uris.Add(uri.GetString()!);
                        }

                        Walk(property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in node.EnumerateArray()) {
                        Walk(item);
                    }

                    break;
            }
        }
    }

    JsonDocument Check() {
        _scratch.InitialiseGit();
        _scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");
        _scratch.WriteText(Path.Combine("src", "Deep", "Nested", "A.cs"), "class C{void M(){M();}}\n");
        _scratch.WriteText(Path.Combine("src", "B.cs"), "class D{void M(){M();}}\n");

        var report = Path.Combine(_scratch.Root, "report.sarif");
        var run = _scratch.Run("check", "--load=loose", "--no-cache", "--output", report, ".");

        Assert.True(
            File.Exists(report),
            $"skala check wrote no SARIF. exit={run.ExitCode}\n{run.StandardOutput}\n{run.StandardError}"
        );

        return JsonDocument.Parse(File.ReadAllText(report));
    }

    [Fact]
    public void EveryArtifactUri_IsRelativeAndForwardSlashed() {
        using var document = Check();
        var uris = ArtifactUris(document.RootElement);

        // A report with no locations in it would pass every assertion below vacuously.
        Assert.NotEmpty(uris);

        foreach (var uri in uris) {
            Assert.DoesNotContain('\\', uri);
            Assert.False(Path.IsPathRooted(uri), $"'{uri}' is absolute.");
            Assert.DoesNotContain("..", uri, StringComparison.Ordinal);

            // ⚠ A drive letter is rooted on Windows and merely a scheme-looking prefix elsewhere,
            // so `Path.IsPathRooted` alone would let `C:/src/a.cs` through a Linux run of this test
            // — and Linux is where CI would be green while Windows shipped the defect.
            Assert.False(
                uri.Length > 1 && uri[1] == ':',
                $"'{uri}' carries a drive letter."
            );
        }

        Assert.Contains("src/Deep/Nested/A.cs", uris);
        Assert.Contains("src/B.cs", uris);
    }

    /// <summary>
    /// ⚠ The URIs must round-trip as URI references, which is the actual contract — SARIF 2.1.0
    /// § 3.4.3 says <c>artifactLocation.uri</c> is a URI, and the ingest that reads it will not
    /// repair one.
    /// </summary>
    [Fact]
    public void EveryArtifactUri_ParsesAsARelativeUri() {
        using var document = Check();

        foreach (var uri in ArtifactUris(document.RootElement)) {
            Assert.True(Uri.TryCreate(uri, UriKind.Relative, out _), $"'{uri}' is not a URI reference.");
        }
    }
}
