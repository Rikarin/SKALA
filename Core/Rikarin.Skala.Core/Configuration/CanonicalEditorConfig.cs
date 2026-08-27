using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>What the distribution package says about the payload it carries.</summary>
public sealed record CanonicalManifest(string Version, string Sha256, int Assignments, int Sections);

/// <summary>
///     The canonical <c>.editorconfig</c> every repository shares, and the rule for composing it.
/// </summary>
/// <remarks>
///     ⚠ ADR-001: the canonical file <b>is</b> the Rider export. <see cref="Compose" /> is the whole of
///     the transformation, it is two additions long, and both of them are what <see cref="Fixer" />
///     already does — so the workflow the ADR protects (change a setting in Rider, re-export, publish)
///     survives intact. Nothing here reads, interprets or reorders the export's own lines.
///     <para>
///         The payload is carried twice: embedded in this assembly, so <c>skala config sync</c> works
///         offline with no restore and exactly one version pin (the tool's), and as content in
///         <c>Rikarin.Skala.Canonical</c>, so a repository can pin the canonical in
///         <c>Directory.Packages.props</c> next to everything else it pins. Both come from one file on
///         disk, and <c>CanonicalDistributionTests</c> asserts they agree byte for byte.
///     </para>
/// </remarks>
public static class CanonicalEditorConfig {
    /// <summary>The name the payload has in the package and in the repository that generates it.</summary>
    public const string PayloadFileName = "canonical.editorconfig";

    /// <summary>The name of the manifest beside it.</summary>
    public const string ManifestFileName = "canonical.json";

    /// <summary>
    ///     The advisory preamble. It is part of the hashed payload deliberately: it is the only thing a
    ///     reader who opens a 4 240-line managed block has to go on, and a payload whose explanation can
    ///     drift from it is a payload with two versions.
    /// </summary>
    public const string Preamble = """
                                   # ==============================================================================
                                   # Skala canonical .editorconfig — managed block.
                                   #
                                   # Everything between the `skala:canonical begin` and `skala:canonical end`
                                   # markers is written by `skala config sync` and verified by
                                   # `skala config diff --canonical`. An edit here is drift (SK9008) and fails the
                                   # gate. The edit you want almost certainly belongs below `skala:local begin`,
                                   # where editorconfig's own later-section-wins rule lets it override this block.
                                   #
                                   # This block is the Rider export (ADR-001) verbatim, with the two additions
                                   # `skala config fix` makes: `root = true`, so the chain stops at the repository
                                   # instead of picking up an .editorconfig above it, and `max_line_length` beside
                                   # `resharper_csharp_max_line_length`, so that tools other than ReSharper can see
                                   # the column limit.
                                   #
                                   # To change it: change the setting in Rider, re-export over
                                   # `editor_config_template` in the Skala repository, run `./build.sh Canonical`,
                                   # publish `Rikarin.Skala.Canonical`, then run `skala config sync` in each
                                   # repository at whatever moment that repository is ready for the reformat.
                                   # ==============================================================================
                                   """;

    static readonly Lazy<string> PayloadText = new(static () => Read(PayloadFileName));
    static readonly Lazy<CanonicalManifest> ManifestValue = new(static () => ReadManifest(Read(ManifestFileName)));

    /// <summary>The canonical payload this build of Skala carries.</summary>
    public static string Text => PayloadText.Value;

    /// <summary>Its version, its hash, and the shape of the export it was made from.</summary>
    public static CanonicalManifest Manifest => ManifestValue.Value;

    /// <summary>
    ///     The canonical payload for a Rider export. This is the entire generation step; the
    ///     <c>Canonical</c> build target is a call to it.
    /// </summary>
    public static string Compose(string templateText) {
        var document = EditorConfigDocument.FromText(PayloadFileName, templateText);
        var reconciled = Fixer.Fix(document).Text;
        return Normalize(Preamble + "\n" + reconciled);
    }

    /// <summary>
    ///     The identity of a payload: SHA-256 over its LF-normalised UTF-8 bytes, lowercase hex.
    /// </summary>
    /// <remarks>
    ///     ⚠ Normalising before hashing is what lets a repository cloned with <c>core.autocrlf=true</c>
    ///     verify against the same hash a Linux CI runner computes. A hash over raw bytes would make
    ///     "this repository has drifted" mean "this repository is on Windows".
    /// </remarks>
    public static string Hash(string payload) {
        var bytes = Encoding.UTF8.GetBytes(Normalize(payload));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>LF line endings, and exactly one trailing newline.</summary>
    public static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n') + "\n";

    /// <summary>The manifest for a freshly composed payload.</summary>
    public static CanonicalManifest DescribeManifest(string version, string payload) {
        var document = EditorConfigDocument.FromText(PayloadFileName, payload);
        return new CanonicalManifest(
            version,
            Hash(payload),
            document.Assignments.Count(),
            document.Sections.Count(static section => section.Name is not null)
        );
    }

    public static string WriteManifest(CanonicalManifest manifest) {
        var builder = new StringBuilder();
        builder.AppendLine("{");
        builder.Append("  \"version\": \"").Append(manifest.Version).AppendLine("\",");
        builder.Append("  \"sha256\": \"").Append(manifest.Sha256).AppendLine("\",");
        builder.Append("  \"assignments\": ")
            .Append(manifest.Assignments.ToString(CultureInfo.InvariantCulture))
            .AppendLine(",");
        builder.Append("  \"sections\": ")
            .Append(manifest.Sections.ToString(CultureInfo.InvariantCulture))
            .AppendLine(",");
        builder.AppendLine("  \"source\": \"editor_config_template — the Rider export (ADR-001)\",");
        builder.AppendLine("  \"generated\": \"./build.sh Canonical\"");
        builder.AppendLine("}");
        return Normalize(builder.ToString());
    }

    static CanonicalManifest ReadManifest(string json) {
        using var document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }
        );
        var root = document.RootElement;
        return new CanonicalManifest(
            root.GetProperty("version").GetString() ?? "0.0.0",
            root.GetProperty("sha256").GetString() ?? string.Empty,
            root.GetProperty("assignments").GetInt32(),
            root.GetProperty("sections").GetInt32()
        );
    }

    static string Read(string fileName) {
        var name = $"Rikarin.Skala.Core.{fileName}";
        using var stream = typeof(CanonicalEditorConfig).GetTypeInfo().Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"'{name}' is not embedded in Rikarin.Skala.Core. Run `./build.sh Canonical` to generate the distribution payload."
            );

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
