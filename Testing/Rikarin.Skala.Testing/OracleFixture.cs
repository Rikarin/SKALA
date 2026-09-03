using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>The header a committed fixture carries, so that a stale one is visible in a diff.</summary>
public sealed record OracleHeader(string ReSharperVersion, string ConfigHash, string Profile, string Generated) {
    public const string Prefix = "// skala-oracle:";

    public override string ToString() =>
        $"{Prefix} resharper={ReSharperVersion} config=sha256:{ConfigHash} profile={Profile} generated={Generated}";

    public static OracleHeader? Parse(string line) {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal)) {
            return null;
        }

        var fields = line[Prefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => part.Split('=', 2))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(static parts => parts[0], static parts => parts[1], StringComparer.Ordinal);

        return fields.TryGetValue("resharper", out var version)
            ? new OracleHeader(
                version,
                fields.GetValueOrDefault("config", string.Empty)
                    .Replace("sha256:", string.Empty, StringComparison.Ordinal),
                fields.GetValueOrDefault("profile", string.Empty),
                fields.GetValueOrDefault("generated", string.Empty)
            )
            : null;
    }
}

/// <summary>
///     Reads and writes the committed <c>jb cleanupcode</c> fixtures.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/12 § "The oracle": regenerating a fixture on failure is forbidden. An oracle that
///     updates itself when it disagrees is a tautology. Only <c>./build.sh Oracle</c> writes here, and
///     its diff is reviewed in its own commit.
/// </remarks>
public static class OracleFixture {
    /// <summary>The format-only fixture's body, with the header line removed.</summary>
    public static string Read(CorpusFile file) => Read(file, OracleProfile.FormatOnly);

    /// <summary>One profile's fixture body, with the header line removed.</summary>
    public static string Read(CorpusFile file, OracleProfile profile) =>
        StripHeader(File.ReadAllText(file.ExpectedPathFor(profile)));

    static string StripHeader(string text) {
        var newLine = text.IndexOf('\n');
        return newLine >= 0 && text.StartsWith(OracleHeader.Prefix, StringComparison.Ordinal)
            ? text[(newLine + 1)..]
            : text;
    }

    public static OracleHeader? ReadHeader(CorpusFile file) => ReadHeader(file, OracleProfile.FormatOnly);

    public static OracleHeader? ReadHeader(CorpusFile file, OracleProfile profile) {
        var path = file.ExpectedPathFor(profile);
        if (!File.Exists(path)) {
            return null;
        }

        using var reader = new StreamReader(path);
        return reader.ReadLine() is { } line ? OracleHeader.Parse(line) : null;
    }

    /// <summary>The body of a variant fixture, with the header line removed.</summary>
    public static string Read(CorpusFile file, CorpusVariant variant) {
        var text = File.ReadAllText(variant.FixturePath(file));
        var newLine = text.IndexOf('\n');
        return newLine >= 0 && text.StartsWith(OracleHeader.Prefix, StringComparison.Ordinal)
            ? text[(newLine + 1)..]
            : text;
    }

    /// <summary>Only <c>./build.sh Oracle</c> calls this.</summary>
    public static void Write(CorpusFile file, string body, OracleHeader header) =>
        File.WriteAllText(file.ExpectedPath, header + "\n" + body);

    /// <summary>Only <c>./build.sh Oracle</c> calls this.</summary>
    public static void Write(CorpusFile file, OracleProfile profile, string body, OracleHeader header) =>
        File.WriteAllText(file.ExpectedPathFor(profile), header + "\n" + body);

    /// <summary>Only <c>./build.sh Oracle</c> calls this.</summary>
    public static void Write(CorpusFile file, CorpusVariant variant, string body, OracleHeader header) =>
        File.WriteAllText(variant.FixturePath(file), header + "\n" + body);

    /// <summary>The declared divergences on a fixture, which turn a difference into a decision.</summary>
    public static IReadOnlyList<string> Divergences(CorpusFile file) {
        if (!File.Exists(file.ExpectedPath)) {
            return [];
        }

        var found = new List<string>();
        foreach (var line in File.ReadLines(file.ExpectedPath).Take(8)) {
            const string marker = "// skala-divergence:";
            if (line.StartsWith(marker, StringComparison.Ordinal)) {
                found.Add(line[marker.Length..].Trim());
            }
        }

        return found;
    }

    /// <summary>
    ///     The configuration digest a fixture header records: SHA256 over the file's bytes, first 16 hex.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The only implementation, and deliberately so.</b> This value is written into every fixture
    ///     header, into the key-flip sweep's report, and read back by
    ///     <c>ProvenanceTests</c> to decide whether a committed measurement is still a
    ///     measurement of the configuration on disk. Two implementations of it is precisely how that
    ///     comparison rots: <see cref="KeyFlipSweep" /> used to hash <c>File.ReadAllText</c>'s UTF-8
    ///     re-encoding rather than the bytes, which agrees with this one only for as long as the file has
    ///     no byte-order mark — and the day somebody's editor adds one, the sweep and the fixtures would
    ///     record two different digests for the same file and each would look stale to the other.
    ///     <para>
    ///         ⚠ Bytes, not decoded text, is the right side of that choice: the digest exists to answer "is
    ///         this the same file `jb` was handed", and `jb` is handed the file.
    ///     </para>
    /// </remarks>
    public static string HashConfig(string editorConfigPath) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(editorConfigPath)))[..16];

    /// <summary>The digest of the base configuration as it stands on disk right now.</summary>
    public static string ConfigDigestInForce() => HashConfig(Corpus.OracleEditorConfigPath);

    /// <summary>
    ///     ⚠ Rewrites the recorded configuration digest of every fixture that carries <paramref name="from" />,
    ///     <b>without regenerating a single byte of any fixture body</b>. Returns how many were rewritten.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Read this before you run it.</b> This is the button that makes
    ///     <c>ProvenanceTests.EveryFixture_RecordsTheConfigurationInForce</c> go green without doing any
    ///     work, and used on a hunch it destroys the only evidence that the corpus is stale. It is
    ///     legitimate in exactly one situation: the base configuration changed, and the change has been
    ///     <em>measured</em> not to alter a single byte the oracle produces. Anything less — "it looks
    ///     like only comments moved", "those keys are surely for another language" — is the tautology
    ///     docs/plan/12 § "The oracle" forbids, arriving through the side door.
    ///     <para>
    ///         <b>The one time it has been used, and the measurement that justified it.</b> At
    ///         <c>076fde6</c> the base configuration lost ~1 997 hand-stripped keys and its digest moved
    ///         <c>98ff5257</c> → <c>bd9791d3</c>. Of the 41 removed lines that are not <c>resharper_cpp_*</c>,
    ///         <c>resharper_vb_*</c> or <c>resharper_c_*</c>, 39 are F# and two are global ReSharper keys that a
    ///         C# run could in principle read: <c>skala_autodetect_indent_settings = false</c> and
    ///         <c>skala_use_old_engine = false</c>. Losing the first looks like the hazard SK9006 exists
    ///         for — a key whose absence hands indent autodetection back to a ReSharper default.
    ///     </para>
    ///     <para>
    ///         ⚠ It is not, and the reason is worth recording because the obvious reading is the wrong one
    ///         in two separate places. JetBrains publishes no default for either key, so both were read out
    ///         of the shipped <c>GlobalTools 2025.2.6</c> assemblies:
    ///         <c>CommonFormatterSettingsKey.AUTODETECT_INDENT_SETTINGS</c> carries
    ///         <c>[SettingsEntry(false, …)]</c> — the default <em>is</em> <c>false</c>, so the stripped line
    ///         was asserting the value already in force. And <c>skala_use_old_engine</c> is not the C#
    ///         key at all: it is <c>HtmlFormatterSettingsKey.UseOldEngine</c>, "use old engine for Razor
    ///         formatting", also defaulting to <c>false</c>. The C# one is <c>skala_old_engine</c>,
    ///         which this repository has never set.
    ///     </para>
    ///     <para>
    ///         It was measured rather than argued. Thirty-three files — the 25
    ///         <c>constructs/indentation/</c> inputs, <c>pathological/tabs-in-a-spaces-file.cs</c>, a
    ///         scrambled mixed-indent Newtonsoft file, four <c>real/</c> files, and two purpose-built
    ///         autodetection baits consistently indented at two spaces and at tabs — were run through
    ///         <c>jb cleanupcode</c> under the pre-strip configuration and under the current one, under
    ///         <b>both</b> oracle profiles. Every one of the 132 outputs was byte-identical. The
    ///         instrument was live while it said so: the format-only profile moved 17 of the 33 files and
    ///         the cleanup profile 20, and a control run with <c>indent_size = 2</c> appended through the
    ///         same override path moved all 33.
    ///     </para>
    ///     <para>
    ///         ⚠ And the cause was established rather than assumed, because this project has been burned
    ///         once by reading a limitation of the oracle <em>profile</em> as a property of the tool
    ///         (SK-DIV-0006). The tempting explanation — that the surviving
    ///         <c>skala_apply_auto_detected_rules = false</c> neutralises the lost key — is <b>not</b>
    ///         what the measurement shows, and it is not even the right mechanism:
    ///         <c>ApplyAutoDetectedRules</c> lives on <c>ClrLanguageNamingSettingsKeyBase</c> and governs
    ///         auto-detected <em>naming</em> rules, with no path to the indent services at all. What the
    ///         measurement shows is stronger: forcing all three of
    ///         <c>skala_autodetect_indent_settings</c>, <c>skala_apply_auto_detected_rules</c> and
    ///         <c>skala_use_indent_from_vs</c> to <c>true</c> also produced byte-identical output, and
    ///         the tab- and two-space baits still came back at four spaces. <c>jb cleanupcode</c> performs
    ///         no indentation autodetection at all, at any setting of those keys, under either profile —
    ///         which matches ReSharper's own documented carve-outs, since autodetection is skipped for a
    ///         whole-file reformat and skipped again whenever an <c>.editorconfig</c> covers the file, and
    ///         cleanup is always both. The keys are inert <em>for the oracle</em>.
    ///     </para>
    ///     <para>
    ///         ⚠ "Inert for the oracle" is the whole claim and it is narrower than it sounds. It says
    ///         nothing about Rider, where autodetection is a real feature and where SK9006's warning
    ///         still stands; the IDE was not measured and cannot be measured from here. What it does
    ///         license is exactly this re-stamp: a fixture records what the oracle produced, and the
    ///         oracle produces the same bytes under both configurations.
    ///     </para>
    /// </remarks>
    /// <param name="from">The digest to replace. A fixture recording anything else is left alone.</param>
    /// <param name="to">The digest to write, which must be the one in force.</param>
    public static int Restamp(string from, string to) {
        if (!string.Equals(to, ConfigDigestInForce(), StringComparison.Ordinal)) {
            throw new ArgumentException(
                $"refusing to stamp sha256:{to}, which is not the digest of {Corpus.OracleEditorConfigPath} "
                + $"(sha256:{ConfigDigestInForce()}). A header may only ever record a configuration that exists.",
                nameof(to)
            );
        }

        var restamped = 0;
        foreach (var path in Corpus.Fixtures()) {
            var text = File.ReadAllText(path);
            var newLine = text.IndexOf('\n');
            if (newLine < 0 || OracleHeader.Parse(text[..newLine]) is not { } header) {
                continue;
            }

            if (!string.Equals(header.ConfigHash, from, StringComparison.Ordinal)) {
                continue;
            }

            // ⚠ Line 1 only, and the body copied across untouched. The point of the operation is that
            // the body does not change; rewriting the file from a parsed representation would risk
            // normalising a line ending in a corpus that is marked `-text` precisely because several
            // fixtures are about one.
            File.WriteAllText(path, header with { ConfigHash = to } + text[newLine..]);
            restamped++;
        }

        return restamped;
    }

    public static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
///     A tiny helper so a fixture body can be compared after normalising the line endings the oracle
///     happens to have written.
/// </summary>
public static class TextNormalisation {
    public static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    public static string Normalise(string text) => string.Join('\n', Lines(text));

    public static StringBuilder AppendEscaped(this StringBuilder builder, string value) =>
        builder.Append(value.Replace("\t", """\t""", StringComparison.Ordinal));
}
