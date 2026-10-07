using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     A source file whose bytes are not valid in the encoding its byte-order mark (or the lack of one)
///     declares. Nothing that holds one of these may write the file (<c>SK9018</c>).
/// </summary>
/// <remarks>
///     ⚠ Deliberately <b>not</b> an <see cref="IOException" /> (and <see cref="InvalidDataException" />,
///     the obvious name for it, is sealed). The loaders treat an <see cref="IOException" /> as "the
///     file vanished between enumeration and the read" and drop it without a word
///     (<c>SourceFiles.Open</c>), and every verb's per-file catch reports one as <c>SK9015</c>, "could
///     not be read" — which sends the reader to check permissions over a file that opened perfectly
///     well. A read site that has not been taught this exception crashes loudly instead of reporting
///     the wrong cause or none, and a crash is the failure a test finds.
/// </remarks>
public sealed class UndecodableSourceException(string path, string encoding, long offset, int line, string bytes)
    : Exception(Describe(encoding, offset, bytes)) {
    public string Path { get; } = path;

    /// <summary>The encoding the file declared (by its BOM, or UTF-8 when it has none).</summary>
    public string Encoding { get; } = encoding;

    /// <summary>The offset of the first undecodable byte, counted from the start of the file.</summary>
    public long Offset { get; } = offset;

    /// <summary>The 1-based line the first undecodable byte is on.</summary>
    public int Line { get; } = line;

    /// <summary>The refusal, as every verb reports it.</summary>
    public SkalaDiagnostic ToDiagnostic() =>
        new(FormatDiagnosticIds.NotDecodable, SkalaSeverity.Error, Message, Path, Line);

    static string Describe(string encoding, long offset, string bytes) =>
        $"the file is not valid {encoding}: {bytes} at byte offset "
        + offset.ToString(CultureInfo.InvariantCulture)
        + " cannot be decoded, so it was left byte-identical; re-save it as "
        + encoding
        + " for Skala to change it";
}

/// <summary>
///     How every verb that may write a source file back reads it: strictly, or not at all.
/// </summary>
/// <remarks>
///     ⚠ #387. <c>SourceText.From(stream)</c> decodes with the <em>replacement</em> fallback, so a
///     Latin-1 <c>"café"</c> came back as <c>"caf"</c> plus U+FFFD, and <c>skala format</c> wrote that
///     over the original. Neither unconditional promise could see it: <c>SK9099</c> compares the token
///     stream <em>after</em> the decode, where both sides hold the same U+FFFD, and U+FFFD inside a
///     literal or a comment parses perfectly well, so <c>SK9010</c> had nothing to say either. ⚠ The
///     token stream is only as trustworthy as the decode that produced it.
///     <para>
///         ⚠ Passing a strict <see cref="UTF8Encoding" /> to <c>SourceText.From</c> is not enough, and
///         that is why this does its own BOM detection. The stream reader underneath switches to its
///         own <c>Encoding.UTF8</c> — replacement fallback — the moment it sees a UTF-8 BOM, so a file
///         that starts with <c>EF BB BF</c> and carries one Latin-1 byte further down was still
///         corrupted. The detection below is the stream reader's, byte for byte (UTF-8, UTF-16 LE/BE,
///         UTF-32 LE/BE), so every file that decoded before decodes to the same text and reports the
///         same encoding now; the only change is that a byte the declared encoding cannot decode
///         throws instead of becoming U+FFFD.
///     </para>
///     <para>
///         ⚠ UTF-16 without a BOM is read as UTF-8, as before #387, and which refusal it gets now
///         depends on its content — deliberately. An ASCII-only one is valid UTF-8 (every byte is below
///         0x80), so it decodes, its NULs fail the parse, and it is <c>SK9010</c> exactly as it was. One
///         holding any character above U+007F is not valid UTF-8 (<c>é</c> is <c>E9 00</c>), and it
///         moves from <c>SK9010</c> to <c>SK9018</c> because that is now the truer statement: the bytes
///         are not text in the encoding the file declares, and the parse never ran on what is in it.
///         Both leave it byte-identical.
///     </para>
/// </remarks>
public static class SourceDecoding {
    /// <summary>Reads <paramref name="path" />, or throws <see cref="UndecodableSourceException" />.</summary>
    public static SourceText Read(string path) => Decode(File.ReadAllBytes(path), path);

    /// <summary>Decodes a file's bytes, or throws <see cref="UndecodableSourceException" />.</summary>
    public static SourceText Decode(byte[] bytes, string path) {
        var (name, strict, reported, bom) = Detect(bytes);
        string text;
        try {
            text = strict.GetString(bytes.AsSpan(bom));
        } catch (DecoderFallbackException exception) {
            // ⚠ Decoded from a span, so the index is relative to the span and nothing else — the
            // array overloads' index has meant different things across runtimes.
            var index = Math.Clamp(exception.Index, 0, bytes.Length - bom);
            var unknown = exception.BytesUnknown is { Length: > 0 } known
                ? known
                : bytes.AsSpan(bom + index, Math.Min(1, bytes.Length - bom - index)).ToArray();

            // ⚠ And the index does not mean the same thing in every decoder. UTF-8 puts it at the
            // first bad byte; UTF-16 discovers an unpaired high surrogate only on reading the unit
            // after it, and puts the index there — two bytes past the bytes it names. Measured, and
            // pinned by the lone-surrogate row in SourceDecodingTests.
            if (!bytes.AsSpan(bom + index).StartsWith(unknown)
                && index >= unknown.Length
                && bytes.AsSpan(bom + index - unknown.Length).StartsWith(unknown)) {
                index -= unknown.Length;
            }

            // Lenient on purpose: the prefix can end inside the very sequence that failed.
            var line = SourceText.From(reported.GetString(bytes.AsSpan(bom, index))).Lines.Count;
            throw new UndecodableSourceException(
                path,
                name,
                bom + index,
                line,
                (unknown.Length == 1 ? "byte " : "bytes ")
                + string.Join(' ', unknown.Select(static b => "0x" + b.ToString("X2", CultureInfo.InvariantCulture)))
            );
        }

        return SourceText.From(text, reported);
    }

    /// <summary>
    ///     The stream reader's BOM detection: which encoding decodes the file strictly, which one the
    ///     <see cref="SourceText" /> reports (and so writes back with), and how long the BOM is.
    /// </summary>
    /// <remarks>
    ///     ⚠ The reported encoding is never the strict one. A writer handed a throwing encoder fails on
    ///     the day some text it cannot encode reaches it, and the file-level write that would have been
    ///     the symptom is the place an exception is least wanted. The two differ only in the fallback;
    ///     the preamble — the BOM written back — is the same.
    /// </remarks>
    static (string Name, Encoding Strict, Encoding Reported, int Bom) Detect(ReadOnlySpan<byte> bytes) {
        if (bytes is [0xEF, 0xBB, 0xBF, ..]) {
            return ("UTF-8", new UTF8Encoding(true, true), new UTF8Encoding(true), 3);
        }

        if (bytes is [0xFF, 0xFE, 0x00, 0x00, ..]) {
            return ("UTF-32LE", new UTF32Encoding(false, true, true), new UTF32Encoding(false, true), 4);
        }

        if (bytes is [0xFF, 0xFE, ..]) {
            return ("UTF-16LE", new UnicodeEncoding(false, true, true), new UnicodeEncoding(false, true), 2);
        }

        if (bytes is [0xFE, 0xFF, ..]) {
            return ("UTF-16BE", new UnicodeEncoding(true, true, true), new UnicodeEncoding(true, true), 2);
        }

        if (bytes is [0x00, 0x00, 0xFE, 0xFF, ..]) {
            return ("UTF-32BE", new UTF32Encoding(true, true, true), new UTF32Encoding(true, true), 4);
        }

        return ("UTF-8", new UTF8Encoding(false, true), new UTF8Encoding(false), 0);
    }
}
