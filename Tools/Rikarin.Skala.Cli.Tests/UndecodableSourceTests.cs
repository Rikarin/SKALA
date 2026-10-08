using Rikarin.Skala.Testing;
using System.Text;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     #387 through the real binary: a file whose bytes its declared encoding cannot decode is
///     refused by every verb that could write it (<c>SK9018</c>), and the ones that can are
///     written back in the encoding they came in.
/// </summary>
/// <remarks>
///     ⚠ The verbs are not one code path. <c>format</c> and <c>arrange</c> read through
///     <c>CSharpFormatter.Read</c>, <c>verify</c>'s formatting stage through <c>FormattingFindings</c>,
///     and <c>fix</c> read with <c>File.ReadAllText</c> and wrote a fixed BOM-less UTF-8 — so before
///     #387 it corrupted a Latin-1 file <em>and</em> stripped a BOM <em>and</em> turned UTF-16 into
///     UTF-8, three defects the formatter's own tests could never see.
/// </remarks>
public sealed class UndecodableSourceTests : IDisposable {
    // `is not { }` is SK2173, whose fix is marked safe: the cheapest way to make `fix` write a file.
    const string Source =
        "sealed class C {\n"
        + "    public string Cafe = \"café naïve\";\n\n"
        + "    public static bool M(object result) {\n"
        + "        if (result is not { }) {\n"
        + "            return false;\n"
        + "        }\n\n"
        + "        return true;\n"
        + "    }\n"
        + "}\n";

    readonly string directory = Directory.CreateTempSubdirectory("skala-decode-cli-").FullName;

    public UndecodableSourceTests() {
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
    }

    public void Dispose() => Directory.Delete(directory, true);

    string WriteFile(string name, byte[] bytes) {
        var path = Path.Combine(directory, name + ".cs");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    ///     ⚠ Already formatted, deliberately: the case that hid the bug, because <c>format</c> wrote
    ///     nothing for it and reported nothing. <c>--check</c> must now fail on it rather than pass.
    /// </summary>
    [Theory]
    [InlineData("format")]
    [InlineData("format", "--check")]
    [InlineData("arrange")]
    [InlineData("arrange", "--check")]
    [InlineData("verify", "--load=loose")]
    [InlineData("check", "--load=loose")]
    public void EveryVerb_RefusesALatin1File_AndExitsInternalError(params string[] verb) {
        var path = WriteFile("Latin1", Encoding.Latin1.GetBytes(Source));
        var before = File.ReadAllBytes(path);

        var run = CliRunner.Run([..verb, path]);

        Assert.Equal(5, run.ExitCode);
        Assert.Contains("SK9018", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>
    ///     <c>verify</c>'s banner names the cause as the repository's, never as a Skala bug — the default
    ///     an unclassified error id falls to.
    /// </summary>
    [Fact]
    public void Verify_NamesTheEncoding_NotASkalaBug() {
        var path = WriteFile("Banner", Encoding.Latin1.GetBytes(Source));

        var run = CliRunner.Run("verify", "--load=loose", path);

        Assert.Contains("not valid in its encoding", run.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Skala bug", run.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Fix_RefusesALatin1File_AndWritesNothing() {
        var path = WriteFile("FixLatin1", Encoding.Latin1.GetBytes(Source));
        var before = File.ReadAllBytes(path);

        var run = CliRunner.Run("fix", "--load=loose", path);

        Assert.Contains("SK9018", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("applied 0 fixes", run.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    public static TheoryData<string> Encodings => ["utf8", "utf8-bom", "utf16le-bom", "utf16be-bom"];

    /// <summary>
    ///     ⚠ The other half of what <c>fix</c> got wrong: a valid file came back as BOM-less UTF-8
    ///     whatever it went in as. Measured before the change on all three non-UTF-8-plain rows.
    /// </summary>
    [Theory]
    [MemberData(nameof(Encodings))]
    public void Fix_WritesAValidFileBack_InTheEncodingItCameIn(string name) {
        var (preamble, encoding) = name switch {
            "utf8" => (Array.Empty<byte>(), (Encoding)new UTF8Encoding(false)),
            "utf8-bom" => (new byte[] { 0xEF, 0xBB, 0xBF }, new UTF8Encoding(false)),
            "utf16le-bom" => ([0xFF, 0xFE], new UnicodeEncoding(false, false)),
            "utf16be-bom" => ([0xFE, 0xFF], new UnicodeEncoding(true, false)),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        var path = WriteFile("Fix-" + name, [..preamble, ..encoding.GetBytes(Source)]);

        var run = CliRunner.Run("fix", "--load=loose", path);

        Assert.Contains("applied 1 fix", run.StandardOutput, StringComparison.Ordinal);
        var after = File.ReadAllBytes(path);
        Assert.Equal(preamble, after.AsSpan(0, preamble.Length).ToArray());
        var text = encoding.GetString(after.AsSpan(preamble.Length));
        Assert.Contains("\"café naïve\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("is not { }", text, StringComparison.Ordinal);
    }
}
