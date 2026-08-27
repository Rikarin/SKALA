namespace Rikarin.Skala.Reporting.Tests;

/// <summary>
///     <see cref="SarifWriter.Relative" /> — the one function every surface displays a path through.
/// </summary>
/// <remarks>
///     ⚠ It was three lines and wrong three ways, and the symptom was a <c>FORMAT</c> line of absolute
///     paths against an output doc 10 caps at 8 000 characters. Two of the three are also entries on
///     doc 12 § "Cross-platform"'s list of Windows hazards, reached through the reporting layer rather
///     than through the cache key everyone expects them in.
/// </remarks>
public sealed class RelativePathTests {
    static string Root => OperatingSystem.IsWindows() ? @"C:\src\repo" : "/src/repo";

    static string Under(params string[] segments) {
        var result = Root;
        foreach (var segment in segments) {
            result = Path.Combine(result, segment);
        }

        return result;
    }

    [Fact]
    public void APathUnderTheRoot_IsRelativeAndForwardSlashed() {
        Assert.Equal("src/Core/A.cs", SarifWriter.Relative(Root, Under("src", "Core", "A.cs")));
    }

    /// <summary>
    ///     ⚠ doc 12 § "Cross-platform": case-insensitive path comparison. On Windows and on a
    ///     case-insensitive macOS volume the root and the file routinely differ in case because they
    ///     came from different APIs — and the old ordinal prefix test then printed every path in the
    ///     report absolute. On Linux the two really are different files, and it must not fold.
    /// </summary>
    [Fact]
    public void ARootWhoseCaseDiffers_IsStillTheRoot_WhereTheFileSystemSaysSo() {
        var shouted = OperatingSystem.IsWindows() ? @"C:\SRC\REPO" : "/SRC/REPO";

        var actual = SarifWriter.Relative(shouted, Under("a.cs"));

        if (OperatingSystem.IsLinux()) {
            Assert.Equal(Under("a.cs").Replace('\\', '/'), actual);
        } else {
            Assert.Equal("a.cs", actual);
        }
    }

    /// <summary>
    ///     ⚠ The prefix test had no component boundary, so a sibling directory whose name merely starts
    ///     with the root's rendered as a "repository-relative" path that climbs out of the repository.
    /// </summary>
    [Fact]
    public void ASiblingWhoseNameStartsWithTheRoot_IsNotUnderIt() {
        var sibling = OperatingSystem.IsWindows() ? @"C:\src\repo-old\a.cs" : "/src/repo-old/a.cs";

        var actual = SarifWriter.Relative(Root, sibling);

        Assert.Equal(sibling.Replace('\\', '/'), actual);
        Assert.DoesNotContain("..", actual, StringComparison.Ordinal);
    }

    /// <summary>
    ///     <c>RunReport.RepositoryRoot</c> is nullable and the renderers reach this with it. A renderer
    ///     whose job is to always produce something must not throw on the null.
    /// </summary>
    [Fact]
    public void ANullOrEmptyRoot_ReturnsThePathRatherThanThrowing() {
        var path = Under("a.cs");

        Assert.Equal(path.Replace('\\', '/'), SarifWriter.Relative(null, path));
        Assert.Equal(path.Replace('\\', '/'), SarifWriter.Relative(string.Empty, path));
    }

    [Fact]
    public void ATrailingSeparatorOnTheRoot_DoesNotDefeatTheMatch() {
        Assert.Equal("a.cs", SarifWriter.Relative(Root + Path.DirectorySeparatorChar, Under("a.cs")));
    }

    [Fact]
    public void ARelativePath_IsLeftAloneAndForwardSlashed() {
        Assert.Equal("src/A.cs", SarifWriter.Relative(Root, Path.Combine("src", "A.cs")));
    }

    /// <summary>
    ///     ⚠ doc 12 § "Cross-platform": SARIF paths must be repo-relative with forward slashes on every
    ///     OS. A backslash in a SARIF <c>artifactLocation.uri</c> is not a valid URI reference, and the
    ///     GitHub code-scanning ingest silently drops the result rather than reporting the error.
    /// </summary>
    [Fact]
    public void NoSeparatorSurvivesAsABackslash() {
        Assert.DoesNotContain("\\", SarifWriter.Relative(Root, Under("a", "b", "c.cs")), StringComparison.Ordinal);
    }
}
