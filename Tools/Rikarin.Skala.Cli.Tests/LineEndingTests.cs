using System.Text;

namespace Rikarin.Skala.Cli.Tests;

/// <summary>
///     doc 12 § "Cross-platform", hazard 1: CRLF input under <c>end_of_line = lf</c>.
/// </summary>
/// <remarks>
///     ⚠ It is a Windows hazard and it is asserted on every platform, because the bytes are the
///     subject and the bytes do not depend on the host. A file with CRLF endings is what git hands a
///     Windows checkout under <c>core.autocrlf=true</c> and what every Visual Studio template writes;
///     a repository whose <c>.editorconfig</c> says <c>end_of_line = lf</c> then has a formatter that
///     must convert. The failure mode if it does not is not cosmetic: the formatter rewrites every
///     line of every file on one platform and none on the other, and the two halves of a team fight
///     over the whole tree.
///     <para>
///         ⚠ <b>The option that decides this is not <c>end_of_line</c>.</b> doc 04 § "File-level concerns"
///         and <c>PhaseOneOptions</c> both say it: <c>end_of_line</c> is <i>inert</i> while
///         <c>skala_enforce_line_ending_style</c> is false, which is the default — false means an
///         existing ending is preserved, mixed endings included. So the honest reading of the hazard is
///         two assertions, not one, and the first is the one that would be missed by testing only the
///         headline: <c>end_of_line = lf</c> alone must leave CRLF alone.
///     </para>
/// </remarks>
public sealed class LineEndingTests : IDisposable {
    readonly CrossPlatformScratch scratch = new("skala-eol-");

    const string Crlf = "class C{\r\nvoid M(){\r\nM();\r\n}\r\n}\r\n";
    const string Lf = "class C{\nvoid M(){\nM();\n}\n}\n";

    public void Dispose() => scratch.Dispose();

    static (int Crlf, int Lf) Count(string text) {
        var crlf = 0;
        var lf = 0;
        for (var i = 0; i < text.Length; i++) {
            if (text[i] != '\n') {
                continue;
            }

            if (i > 0 && text[i - 1] == '\r') {
                crlf++;
            } else {
                lf++;
            }
        }

        return (crlf, lf);
    }

    void Configure(string endOfLine, bool enforce) =>
        scratch.WriteText(
            ".editorconfig",
            $"root = true\n\n[*.cs]\nend_of_line = {endOfLine}\nskala_enforce_line_ending_style = {(enforce ? "true" : "false")}\n"
        );

    /// <summary>The hazard as doc 12 words it: CRLF in, <c>end_of_line = lf</c>, LF out.</summary>
    [Fact]
    public void CrlfInput_UnderLf_IsWrittenAsLf() {
        Configure("lf", true);
        var path = scratch.WriteText("A.cs", Crlf);

        var run = scratch.Run("format", path);
        Assert.Equal(0, run.ExitCode);

        var formatted = File.ReadAllText(path);
        var (crlf, lf) = Count(formatted);
        Assert.Equal(0, crlf);
        Assert.Equal(5, lf);
        Assert.DoesNotContain('\r', formatted);
    }

    /// <summary>The converse, which is the direction a Windows repository actually configures.</summary>
    [Fact]
    public void LfInput_UnderCrlf_IsWrittenAsCrlf() {
        Configure("crlf", true);
        var path = scratch.WriteText("B.cs", Lf);

        var run = scratch.Run("format", path);
        Assert.Equal(0, run.ExitCode);

        var formatted = File.ReadAllText(path);
        var (crlf, lf) = Count(formatted);
        Assert.Equal(5, crlf);
        Assert.Equal(0, lf);
    }

    /// <summary>
    ///     ⚠ The half of the hazard the headline hides. <c>end_of_line = lf</c> with enforcement off is
    ///     doc 04's stated behaviour — preserve, do not normalise — and a formatter that converted here
    ///     would rewrite every line of a Windows checkout the moment anyone touched one file.
    /// </summary>
    [Fact]
    public void CrlfInput_UnderLfWithEnforcementOff_KeepsCrlf() {
        Configure("lf", false);
        var path = scratch.WriteText("C.cs", Crlf);

        scratch.Run("format", path);

        var formatted = File.ReadAllText(path);
        var (crlf, lf) = Count(formatted);
        Assert.Equal(5, crlf);
        Assert.Equal(0, lf);
    }

    /// <summary>
    ///     ⚠ <c>--check</c> and <c>format</c> must agree about line endings, or CI reports a file that
    ///     needs formatting forever and formatting it changes nothing. This is the same bytes through
    ///     the read-only path.
    /// </summary>
    [Fact]
    public void Check_AgreesWithFormat_AboutLineEndings() {
        Configure("lf", true);
        var path = scratch.WriteText("D.cs", Crlf);

        Assert.Equal(2, scratch.Run("format", "--check", path).ExitCode);

        scratch.Run("format", path);

        // And now nothing is left to do — the second pass is the one that catches a converter that
        // reports an edit it does not make.
        Assert.Equal(0, scratch.Run("format", "--check", path).ExitCode);
    }

    /// <summary>
    ///     ⚠ doc 04: the BOM is preserved exactly, never added and never removed. It travels with CRLF
    ///     on Windows — every file Visual Studio writes has both — so a line-ending conversion that
    ///     re-encodes the file loses it, and a rule that reads column 1 of line 1 then reads a
    ///     zero-width no-break space.
    /// </summary>
    [Fact]
    public void ABomSurvivesALineEndingConversion() {
        Configure("lf", true);
        var path = scratch.WriteBytes("E.cs", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Crlf)]);

        scratch.Run("format", path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }
}
