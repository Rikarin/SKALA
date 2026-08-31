using System.Text.Json;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     doc 12 § "Cross-platform", hazard 4: long paths.
/// </summary>
/// <remarks>
///     ⚠ 260 is <c>MAX_PATH</c>, and it is not a .NET limit — .NET Core prefixes <c>\\?\</c> internally
///     and reaches ~32 767 — but it is still the limit of every Win32 API called without that prefix,
///     which includes anything the tool shells out to and anything a native dependency opens. A deep
///     solution under a deep checkout directory clears 260 easily; a CI agent whose workspace is
///     <c>D:\a\_work\1\s</c> plus a generated-source path clears it without trying.
///     <para>
///         ⚠ The interesting failure is not that the formatter throws. It is that the tool <i>catches</i>
///         the <c>PathTooLongException</c>, counts the file as unreadable, and reports a clean tree — the
///         deep files silently stop being analysed and the report says zero findings, which is the shape of
///         failure a static analyser is least able to survive. So this asserts findings were produced for
///         the long path, not merely that nothing threw.
///     </para>
/// </remarks>
public sealed class LongPathTests : IDisposable {
    readonly CrossPlatformScratch scratch = new("skala-long-");

    // ⚠ 34 characters per segment, not 300: every file system caps a single *component* at 255, so
    // a long path has to be built out of many ordinary-length ones. Eight of these plus the
    // temporary root and the file name clears 260 comfortably on every platform.
    const string Segment = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public void Dispose() => scratch.Dispose();

    /// <summary>
    ///     The deep relative path, or <c>null</c> when the host refuses to create it.
    /// </summary>
    /// <remarks>
    ///     ⚠ A Windows machine without <c>LongPathsEnabled</c> genuinely cannot hold this file, and doc
    ///     12's instruction is that such a test skips cleanly rather than failing. That is the only
    ///     skip here, it is decided by trying rather than by asking <c>OperatingSystem</c>, and the
    ///     assertions below are identical on the platforms that can.
    /// </remarks>
    string? Deep(string fileName) {
        var relative = Path.Combine(Enumerable.Repeat(Segment, 8).ToArray());
        var full = Path.Combine(scratch.Root, relative, fileName);
        Assert.True(full.Length > 260, $"the fixture is only {full.Length} characters long.");

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "class C{void M(){M();}}\n");
            return full;
        } catch (Exception exception) when (exception is PathTooLongException
                                                or DirectoryNotFoundException
                                                or IOException) {
            return null;
        }
    }

    [Fact]
    public void Format_RewritesAFileMoreThan260CharactersDeep() {
        scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");
        if (Deep("Formatted.cs") is not { } path) {
            return;
        }

        var run = scratch.Run("format", path);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("class C {\n    void M() {\n        M();\n    }\n}\n", File.ReadAllText(path));
    }

    [Fact]
    public void FormatCheck_SeesAFileMoreThan260CharactersDeep() {
        scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");
        if (Deep("Checked.cs") is not { } path) {
            return;
        }

        var run = scratch.Run("format", "--check", path);

        // ⚠ Exit 2, not 0. A tool that could not open the file would also print "0 files would be
        // reformatted" and exit 0, and that is the answer this test exists to reject.
        Assert.Equal(2, run.ExitCode);
        Assert.Contains("1 file would be reformatted", run.StandardOutput, StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ And through analysis and the SARIF writer, which is where a long path meets the other
    ///     three hazards at once: it has to be found by the directory walk, analysed, made relative to
    ///     the repository root, and written with forward slashes.
    /// </summary>
    [Fact]
    public void Check_AnalysesALongPathAndWritesItRelativeIntoTheSarif() {
        scratch.InitialiseGit();
        scratch.WriteText(".editorconfig", "root = true\n\n[*.cs]\nindent_size = 4\n");
        if (Deep("Analysed.cs") is null) {
            return;
        }

        var report = Path.Combine(scratch.Root, "report.sarif");
        var run = scratch.Run("check", "--load=loose", "--no-cache", "--output", report, ".");

        Assert.True(
            File.Exists(report),
            $"skala check wrote no SARIF. exit={run.ExitCode}\n{run.StandardOutput}\n{run.StandardError}"
        );

        var text = File.ReadAllText(report);
        using var document = JsonDocument.Parse(text);

        var expected = string.Join('/', Enumerable.Repeat(Segment, 8)) + "/Analysed.cs";
        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.DoesNotContain(scratch.Root.Replace('\\', '/'), text, StringComparison.Ordinal);
    }
}
