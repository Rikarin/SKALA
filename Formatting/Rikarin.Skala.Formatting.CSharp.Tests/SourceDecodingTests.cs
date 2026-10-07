using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp.Arrangement;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp.Tests;

/// <summary>
///     #387: a file whose bytes are not valid in the encoding it declares is refused (<c>SK9018</c>)
///     and left byte-identical; every file that is valid reads exactly as it did before.
/// </summary>
/// <remarks>
///     ⚠ The regression set is the half that matters most. The strict read replaced the reader every
///     verb uses, so a valid file that now decodes to different text, or reports an encoding with a
///     different preamble, would be rewritten with a different BOM or none — the bug this fixes, moved.
///     <para>
///         ⚠ Sabotage: put <c>SourceText.From(stream, canBeEmbedded: false)</c> back into
///         <see cref="CSharpFormatter.Read" /> and every must-fire case goes red, the formatted Latin-1
///         file included — the one that hid the bug, because nothing was ever written for it.
///     </para>
/// </remarks>
public sealed class SourceDecodingTests : IDisposable {
    const string Unformatted = "class C\n{\n    public string Cafe = \"café naïve\";\n  void M( ) { }\n}\n";
    const string Formatted = "class C {\n    public string Cafe = \"café naïve\";\n}\n";
    const string InAComment = "// café\nclass C\n{\n  void M( ) { }\n}\n";

    /// <summary>U+FFFD, the character the lenient decode put where every bad byte was.</summary>
    const char Replacement = (char)0xFFFD;

    static readonly Encoding Latin1 = Encoding.Latin1;

    readonly string directory = Directory.CreateTempSubdirectory("skala-decode-").FullName;

    public void Dispose() => Directory.Delete(directory, true);

    /// <summary>Every file the strict read must accept: the encoding, and the BOM it writes back.</summary>
    public static TheoryData<string> Valid => [
        "utf8", "utf8-bom", "utf16le-bom", "utf16be-bom", "utf32le-bom", "utf16le-nobom-ascii"
    ];

    /// <summary>Every file the strict read must refuse, with the 1-based line of the first bad byte.</summary>
    public static TheoryData<string, int, long> Invalid =>
        new() {
            { "latin1-string", 3, 39 },
            { "latin1-comment", 1, 6 },
            { "latin1-formatted", 2, 39 },
            { "utf8-bom-then-latin1", 3, 47 },
            { "utf16le-nobom-nonascii", 3, 78 },
            { "utf16le-bom-lone-surrogate", 3, 80 }
        };

    static byte[] Bytes(string name) =>
        name switch {
            "utf8" => Encoding.UTF8.GetBytes(Unformatted),
            "utf8-bom" => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Unformatted)],
            "utf16le-bom" => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(Unformatted)],
            "utf16be-bom" => [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(Unformatted)],
            "utf32le-bom" => [0xFF, 0xFE, 0x00, 0x00, .. Encoding.UTF32.GetBytes(Unformatted)],
            "utf16le-nobom-ascii" => Encoding.Unicode.GetBytes("class C\n{\n  void M( ) { }\n}\n"),
            "latin1-string" => Latin1.GetBytes(Unformatted),
            "latin1-comment" => Latin1.GetBytes(InAComment),
            "latin1-formatted" => Latin1.GetBytes(Formatted),
            // A valid `é` first, then a Latin-1 `ï`: the BOM must not switch the reader to a lenient one.
            "utf8-bom-then-latin1" => [
                0xEF, 0xBB, 0xBF,
                .. Encoding.UTF8.GetBytes(Unformatted[..Unformatted.IndexOf('ï', StringComparison.Ordinal)]),
                .. Latin1.GetBytes(Unformatted[Unformatted.IndexOf('ï', StringComparison.Ordinal)..])
            ],
            "utf16le-nobom-nonascii" => Encoding.Unicode.GetBytes(Unformatted),
            // An unpaired high surrogate where the `é` was: UTF-16 has its own invalid sequences.
            "utf16le-bom-lone-surrogate" => [
                0xFF, 0xFE,
                .. Encoding.Unicode.GetBytes(Unformatted[..Unformatted.IndexOf('é', StringComparison.Ordinal)]),
                0x00, 0xD8,
                .. Encoding.Unicode.GetBytes(Unformatted[(Unformatted.IndexOf('é', StringComparison.Ordinal) + 1)..])
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };

    string WriteFile(string name) {
        var path = Path.Combine(directory, name + ".cs");
        File.WriteAllBytes(path, Bytes(name));
        return path;
    }

    /// <summary>
    ///     ⚠ Against the reader this replaced, not against a hand-written expectation: same text, same
    ///     preamble. That is the whole regression claim — a valid file cannot tell the two apart.
    /// </summary>
    [Theory]
    [MemberData(nameof(Valid))]
    public void AValidFile_ReadsExactlyAsTheLenientReaderReadIt(string name) {
        var path = WriteFile(name);

        var strict = CSharpFormatter.Read(path);
        using var stream = File.OpenRead(path);
        var lenient = SourceText.From(stream, canBeEmbedded: false);

        Assert.Equal(lenient.ToString(), strict.ToString());
        Assert.Equal(lenient.Encoding!.GetPreamble(), strict.Encoding!.GetPreamble());
        Assert.DoesNotContain(Replacement, strict.ToString());
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void AnUndecodableFile_IsRefused_AtTheFirstBadByte(string name, int line, long offset) {
        var path = WriteFile(name);

        var refused = Assert.Throws<UndecodableSourceException>(() => CSharpFormatter.Read(path));

        Assert.Equal(line, refused.Line);
        Assert.Equal(offset, refused.Offset);
        var diagnostic = refused.ToDiagnostic();
        Assert.Equal(FormatDiagnosticIds.NotDecodable, diagnostic.Id);
        Assert.Equal(SkalaSeverity.Error, diagnostic.Severity);
        Assert.Equal(path, diagnostic.File);
        Assert.Equal(line, diagnostic.Line);
    }

    /// <summary>
    ///     ⚠ Pins why <see cref="SourceDecoding" /> detects the BOM itself. Handing Roslyn a strict
    ///     UTF-8 encoding looks like the one-line fix and is not: the reader underneath switches to its
    ///     own lenient UTF-8 when it sees <c>EF BB BF</c>, so the bad byte becomes U+FFFD all the same.
    ///     If this ever goes red the one-line fix has started working and the detection can go.
    /// </summary>
    [Fact]
    public void AStrictEncodingHandedToRoslyn_StillDecodesABomFileLeniently() {
        using var stream = new MemoryStream(Bytes("utf8-bom-then-latin1"));

        var text = SourceText.From(stream, new UTF8Encoding(false, true), canBeEmbedded: false);

        Assert.Contains(Replacement, text.ToString());
    }

    /// <summary>
    ///     <c>format</c>, <c>format --check</c>, <c>arrange</c> and <c>arrange --check</c>: refused,
    ///     reported, failed, and not one byte written.
    /// </summary>
    [Theory]
    [MemberData(nameof(Invalid))]
    public void EveryWritingVerb_LeavesAnUndecodableFileByteIdentical(string name, int line, long offset) {
        _ = offset;
        var path = WriteFile(name);
        var before = File.ReadAllBytes(path);

        foreach (var check in new[] { false, true }) {
            var format = FormatCommand.Run(
                new FormatRequest { Paths = [path], RepositoryRoot = directory, Check = check }
            );
            var arrange = ArrangeCommand.Run(
                new ArrangeRequest { Paths = [path], RepositoryRoot = directory, Check = check },
                TestContext.Current.CancellationToken
            );

            foreach (var result in new[] { format, arrange }) {
                Assert.Equal(ExitCodes.InternalError, result.ExitCode);
                Assert.Contains(
                    $":{line}: error {FormatDiagnosticIds.NotDecodable}:",
                    result.Output,
                    StringComparison.Ordinal
                );
                Assert.Equal(before, File.ReadAllBytes(path));
            }
        }
    }

    /// <summary>
    ///     The regression set through <c>format</c> itself: reformatted, with every non-ASCII character
    ///     and the BOM exactly as they were.
    /// </summary>
    [Theory]
    [MemberData(nameof(Valid))]
    public void Format_RewritesAValidFile_InItsOwnEncoding(string name) {
        var path = WriteFile(name);
        var before = File.ReadAllBytes(path);
        var original = CSharpFormatter.Read(path);

        var result = FormatCommand.Run(new FormatRequest { Paths = [path], RepositoryRoot = directory });

        Assert.DoesNotContain(FormatDiagnosticIds.NotDecodable, result.Output, StringComparison.Ordinal);
        var after = File.ReadAllBytes(path);
        var preamble = original.Encoding!.GetPreamble();
        Assert.Equal(preamble, after.AsSpan(0, preamble.Length).ToArray());

        var text = original.Encoding.GetString(after.AsSpan(preamble.Length));
        if (name == "utf16le-nobom-ascii") {
            // SK9010, as before #387: decodes as UTF-8, and its NULs fail the parse.
            Assert.Contains(FormatDiagnosticIds.NotParseable, result.Output, StringComparison.Ordinal);
            Assert.Equal(before, after);
        } else {
            Assert.Equal(ExitCodes.Ok, result.ExitCode);
            Assert.NotEqual(before, after);
            Assert.Contains("\"café naïve\"", text, StringComparison.Ordinal);
        }
    }
}
