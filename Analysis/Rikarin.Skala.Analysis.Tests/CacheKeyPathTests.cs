using Rikarin.Skala.Analysis.Caching;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Tests;

/// <summary>
///     doc 12 § "Cross-platform", hazard 3: case-insensitive path comparison in the cache key.
/// </summary>
/// <remarks>
///     ⚠ The hazard was real and the cache key had it. <see cref="CacheKey.For" /> hashed the raw UTF-8
///     of the path, so on Windows and on a default (case-insensitive) macOS volume the same file
///     reached through two APIs — MSBuild's spelling of the path and a directory walk's — produced two
///     keys and two entries. The direction of the failure is benign (a miss, never a stale hit) and
///     that is exactly why nothing caught it: the tool stays correct and simply stops being warm, so
///     doc 13's "under 5 s on a 5-file change" quietly becomes a cold run every time and the only
///     symptom is a number in a budget table.
///     <para>
///         The converse matters as much and pulls the other way: on Linux <c>a.cs</c> and <c>A.cs</c> are
///         two files, and folding their keys together would serve one file's findings for the other — a
///         stale hit, which is the failure a cache may never have. So this is not "lowercase the path"; it
///         is "compare the path the way this file system compares it", which is what
///         <see cref="SarifWriter.PathComparison" /> already decides for the reporting layer.
///     </para>
/// </remarks>
public sealed class CacheKeyPathTests {
    const string Content = "class C { }\n";

    static string Key(string path) => CacheKey.For(path, "class C { }\n"u8, "compilation", "rules", "editorconfig");

    /// <summary>
    ///     ⚠ The hazard as doc 12 words it. This asserts the correct answer on every platform rather
    ///     than skipping on two of the three: on Windows and macOS the two spellings are one file and
    ///     must share a key; on Linux they are two files and must not.
    /// </summary>
    [Fact]
    public void TwoSpellingsOfOnePath_ShareAKeyExactlyWhereTheFileSystemSaysTheyAreOneFile() {
        var (shouted, whispered) = OperatingSystem.IsWindows()
            ? (@"C:\Src\A.cs", @"c:\src\a.cs")
            : ("/Src/A.cs", "/src/a.cs");

        if (OperatingSystem.IsLinux()) {
            Assert.NotEqual(Key(shouted), Key(whispered));
        } else {
            Assert.Equal(Key(shouted), Key(whispered));
        }
    }

    /// <summary>
    ///     ⚠ Separators are a second, independent decision, and it is Windows-only. Both <c>/</c> and
    ///     <c>\</c> separate on Windows, so the two spellings are one file there. On Unix a backslash
    ///     is an ordinary character in a file name, and folding it would merge two real files — the
    ///     same stale hit reached from the other side.
    /// </summary>
    [Fact]
    public void TwoSeparatorSpellings_ShareAKeyOnlyOnWindows() {
        const string backslashed = @"C:\src\sub\A.cs";
        const string forwardSlashed = "C:/src/sub/A.cs";

        if (OperatingSystem.IsWindows()) {
            Assert.Equal(Key(backslashed), Key(forwardSlashed));
        } else {
            Assert.NotEqual(Key(backslashed), Key(forwardSlashed));
        }
    }

    /// <summary>
    ///     The normalisation must not swallow the distinction it exists to preserve: two genuinely
    ///     different files still get different keys everywhere.
    /// </summary>
    [Fact]
    public void TwoDifferentFiles_NeverShareAKey() {
        var a = Path.Combine("src", "A.cs");
        var b = Path.Combine("src", "B.cs");

        Assert.NotEqual(Key(a), Key(b));
    }

    /// <summary>
    ///     ⚠ The one place the answer is decided, asserted to be the same one place the reporting
    ///     layer uses. Two implementations of "are these the same file" is one more than the number
    ///     that can be right, and the previous defect in <see cref="SarifWriter.Relative" /> was the
    ///     same question answered differently in the other direction.
    /// </summary>
    [Fact]
    public void TheNormalisationFollowsTheHouseComparison() {
        Assert.Equal(
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase,
            SarifWriter.PathComparison
        );

        var normalised = CacheKey.NormalisePath(Path.Combine("Src", "A.cs"));
        if (OperatingSystem.IsLinux()) {
            Assert.Equal(Path.Combine("Src", "A.cs"), normalised);
        } else {
            Assert.Equal(Path.Combine("Src", "A.cs").ToUpperInvariant(), normalised);
        }
    }
}
