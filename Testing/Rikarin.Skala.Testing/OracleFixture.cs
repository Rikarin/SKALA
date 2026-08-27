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
/// Reads and writes the committed <c>jb cleanupcode</c> fixtures.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/12 § "The oracle": regenerating a fixture on failure is forbidden. An oracle that
/// updates itself when it disagrees is a tautology. Only <c>./build.sh Oracle</c> writes here, and
/// its diff is reviewed in its own commit.
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

    public static string HashConfig(string editorConfigPath) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(editorConfigPath)))[..16];

    public static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// A tiny helper so a fixture body can be compared after normalising the line endings the oracle
/// happens to have written.
/// </summary>
public static class TextNormalisation {
    public static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    public static string Normalise(string text) => string.Join('\n', Lines(text));

    public static StringBuilder AppendEscaped(this StringBuilder builder, string value) =>
        builder.Append(value.Replace("\t", "\\t", StringComparison.Ordinal));
}
