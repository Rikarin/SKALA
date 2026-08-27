using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>The machine-readable half of the begin marker.</summary>
public sealed record CanonicalMarker(string Version, string Sha256) {
    public override string ToString() =>
        $"{CanonicalLayout.BeginMarker} version={Version} sha256={Sha256}";
}

/// <summary>
/// A repository's <c>.editorconfig</c> split into the managed canonical block and the local block
/// below it.
/// </summary>
/// <param name="Marker">Null when the file has never been synced.</param>
/// <param name="CanonicalText">The payload between the markers, verbatim. Empty when unmanaged.</param>
/// <param name="LocalText">Everything below the local marker — or the whole file, when unmanaged.</param>
/// <param name="LocalFirstLine">
/// The 1-based line in the real file that <see cref="LocalText"/> starts on, so that a diagnostic
/// about a local override can name a line a reader can go to.
/// </param>
public sealed record CanonicalLayoutResult(
    CanonicalMarker? Marker,
    string CanonicalText,
    string LocalText,
    int LocalFirstLine) {
    public bool IsManaged => Marker is not null;
}

/// <summary>
/// The two-block layout of a managed <c>.editorconfig</c>, and the marker grammar that delimits it.
/// </summary>
/// <remarks>
/// ⚠ There is exactly one file, because Rider reads exactly one file per directory and it is called
/// <c>.editorconfig</c>. Every layering scheme that puts the canonical somewhere else — a second
/// file, an MSBuild item, a <c>.globalconfig</c> from a package — is invisible to the IDE, and an
/// IDE formatting against a different configuration than the gate is the exact failure Skala exists
/// to prevent. So the layering is inside the file, and it is editorconfig's own: the local block
/// comes <em>after</em> the canonical block, and later sections win.
/// </remarks>
public static class CanonicalLayout {
    public const string BeginMarker = "# skala:canonical begin";
    public const string EndMarker = "# skala:canonical end";
    public const string LocalMarker = "# skala:local begin";

    public const string LocalBanner = """
        # ------------------------------------------------------------------------------
        # This repository's own configuration. Skala never writes below this line.
        #
        # editorconfig resolves later sections over earlier ones within a file, so
        # anything here overrides the canonical block above — which is how a legitimate
        # local override survives a canonical version bump.
        # ------------------------------------------------------------------------------
        """;

    /// <summary>Split a file into its canonical and local halves.</summary>
    public static CanonicalLayoutResult Split(string text) {
        var lines = Lines(text);
        var begin = IndexOf(lines, BeginMarker);
        var end = IndexOf(lines, EndMarker);

        if (begin < 0 || end < begin) {
            return new CanonicalLayoutResult(null, string.Empty, CanonicalEditorConfig.Normalize(text), 1);
        }

        var marker = ParseMarker(lines[begin]);
        var canonical = string.Join("\n", lines.Skip(begin + 1).Take(end - begin - 1));

        var localMarker = IndexOf(lines, LocalMarker, end);
        var localStart = localMarker < 0 ? end + 1 : localMarker + 1;

        // ⚠ Skip the blank line Assemble writes between the marker and the local text. Without
        // this, split-then-assemble grows the file by one blank line every sync, which makes sync
        // non-idempotent and turns every run into a diff.
        while (localStart < lines.Count && lines[localStart].Trim().Length == 0) {
            localStart++;
        }

        var local = string.Join("\n", lines.Skip(localStart));

        return new CanonicalLayoutResult(
            marker,
            CanonicalEditorConfig.Normalize(canonical),
            local.Trim().Length == 0 ? string.Empty : CanonicalEditorConfig.Normalize(local),
            localStart + 1);
    }

    /// <summary>
    /// Build the file: marker, payload, marker, banner, local text. LF throughout, because the
    /// canonical sets <c>end_of_line = lf</c> and a managed block that disagrees with itself is a
    /// poor advertisement.
    /// </summary>
    public static string Assemble(string canonicalText, string version, string localText) {
        var payload = CanonicalEditorConfig.Normalize(canonicalText);
        var builder = new StringBuilder();

        builder.Append(new CanonicalMarker(version, CanonicalEditorConfig.Hash(payload)).ToString()).Append('\n');
        builder.Append(payload);
        builder.Append(EndMarker).Append('\n');
        builder.Append('\n');

        // ⚠ The banner goes *above* the local marker, so that the marker is the last line Skala
        // writes. Everything after it is the repository's, byte for byte, and `Split` can hand it
        // back without having to recognise and strip Skala's own prose — which would have to stay
        // in sync with the prose forever.
        builder.Append(LocalBanner.Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');
        builder.Append(LocalMarker).Append('\n');

        var local = localText.Trim().Length == 0 ? string.Empty : CanonicalEditorConfig.Normalize(localText);
        if (local.Length > 0) {
            builder.Append('\n').Append(local);
        }

        return CanonicalEditorConfig.Normalize(builder.ToString());
    }

    /// <summary>
    /// Strip a <c>root</c> assignment from a would-be local block.
    /// </summary>
    /// <remarks>
    /// The canonical block carries <c>root = true</c> in its own preamble. A second <c>root</c> in
    /// the local block sits below a section header, where editorconfig ignores it entirely — so
    /// leaving it in place would leave a line that looks load-bearing and is inert. It is removed,
    /// and <c>sync</c> says so rather than doing it quietly.
    /// </remarks>
    public static string StripRoot(string localText, out bool stripped) {
        stripped = false;
        var kept = new List<string>();
        var inPreamble = true;

        foreach (var line in Lines(localText)) {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('[')) {
                inPreamble = false;
            }

            if (inPreamble && IsRootAssignment(trimmed)) {
                stripped = true;
                continue;
            }

            kept.Add(line);
        }

        return stripped ? string.Join("\n", kept).TrimStart('\n') : localText;
    }

    static bool IsRootAssignment(string trimmed) {
        var separator = trimmed.IndexOf('=', StringComparison.Ordinal);
        return separator > 0 && trimmed[..separator].Trim().Equals("root", StringComparison.OrdinalIgnoreCase);
    }

    static CanonicalMarker ParseMarker(string line) {
        var version = Field(line, "version=") ?? "unknown";
        var sha = Field(line, "sha256=") ?? string.Empty;
        return new CanonicalMarker(version, sha);
    }

    static string? Field(string line, string name) {
        var start = line.IndexOf(name, StringComparison.Ordinal);
        if (start < 0) {
            return null;
        }

        start += name.Length;
        var end = start;
        while (end < line.Length && !char.IsWhiteSpace(line[end])) {
            end++;
        }

        return end > start ? line[start..end] : null;
    }

    static int IndexOf(List<string> lines, string marker, int from = 0) {
        for (var i = Math.Max(0, from); i < lines.Count; i++) {
            if (lines[i].TrimStart().StartsWith(marker, StringComparison.Ordinal)) {
                return i;
            }
        }

        return -1;
    }

    static List<string> Lines(string text) => [
        .. CanonicalEditorConfig.Normalize(text).TrimEnd('\n').Split('\n')
    ];

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
