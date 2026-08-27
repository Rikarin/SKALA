using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>One option's registry entry, as the sweep would change it.</summary>
/// <param name="Key">The option's canonical spelling.</param>
/// <param name="FromDefault">The value <c>options.json</c> records now.</param>
/// <param name="ToDefault">The value the sweep derived.</param>
/// <param name="FromSource">The <c>defaultSource</c> recorded now.</param>
public sealed record RegistryChange(string Key, string? FromDefault, string ToDefault, string FromSource) {
    public bool ChangesValue => !string.Equals(FromDefault, ToDefault, StringComparison.Ordinal);
}

/// <summary>
///     Writes verified defaults back into <c>options.json</c>, one entry at a time.
/// </summary>
/// <remarks>
///     ⚠ A line-level edit rather than a parse-and-reserialise. <c>options.json</c> is written by
///     <c>tools/import-options.py</c> and a round trip through a different serialiser reformats all
///     520 entries — escaping, key order, trailing whitespace — and buries five real changes in a
///     twelve-thousand-line diff. The registry is reviewed in its diff, so the diff has to stay
///     readable; this replaces exactly the two lines it means to and asserts it found them.
///     <para>
///         ⚠ Only <see cref="DefaultsVerdict.Verified" /> is written, and it is written as
///         <c>defaultSource: "oracle-probe"</c> rather than <c>"resharper-docs"</c>. JetBrains still
///         publishes no defaults; this one is derived, and the registry says which it is because
///         <c>skala config distill</c> drops keys on the strength of it.
///     </para>
/// </remarks>
public static class RegistryPatch {
    const string Source = "oracle-probe";

    public static IReadOnlyList<RegistryChange> Plan(string registryPath, IReadOnlyList<DerivedDefault> probed) {
        var text = File.ReadAllText(registryPath);
        var changes = new List<RegistryChange>();

        foreach (var entry in probed.Where(static entry => entry.Verdict == DefaultsVerdict.Verified)) {
            if (Locate(text, entry.Key) is not { } block) {
                continue;
            }

            var currentDefault = Field(text, block, "default");
            var currentSource = Field(text, block, "defaultSource");

            if (string.Equals(currentSource, Source, StringComparison.Ordinal)
                && string.Equals(currentDefault, entry.Value, StringComparison.Ordinal)) {
                continue;
            }

            changes.Add(new RegistryChange(entry.Key, currentDefault, entry.Value!, currentSource ?? "?"));
        }

        return changes;
    }

    public static void Apply(string registryPath, IReadOnlyList<RegistryChange> changes) {
        var text = File.ReadAllText(registryPath);
        foreach (var change in changes) {
            var block = Locate(text, change.Key)
                ?? throw new InvalidOperationException($"{change.Key}: no entry in the registry to patch.");
            text = Replace(text, block, "default", change.ToDefault);

            var moved = Locate(text, change.Key)
                ?? throw new InvalidOperationException($"{change.Key}: the entry vanished mid-patch.");
            text = Replace(text, moved, "defaultSource", Source);
        }

        File.WriteAllText(registryPath, text);
    }

    public static string Render(IReadOnlyList<RegistryChange> changes) {
        var builder = new StringBuilder();
        builder.Append("registry: ")
            .Append(changes.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" entries would gain a verified default");
        builder.Append("  of which the recorded value is wrong: ")
            .Append(changes.Count(static change => change.ChangesValue).ToString(CultureInfo.InvariantCulture))
            .AppendLine();
        builder.AppendLine();

        foreach (var change in changes.OrderBy(static change => change.Key, StringComparer.Ordinal)) {
            builder.Append("  ")
                .Append(change.Key.PadRight(58))
                .Append(change.FromSource.PadRight(14))
                .Append((change.FromDefault ?? "-").PadRight(20))
                .Append("→ ")
                .AppendLine(change.ToDefault);
        }

        return builder.ToString();
    }

    /// <summary>The span of the option object whose <c>"key"</c> is <paramref name="key" />.</summary>
    /// <remarks>
    ///     ⚠ Anchored on the <c>"key"</c> line and bounded by the next one, so the search for
    ///     <c>"default"</c> cannot wander into the neighbouring entry. The importer writes one entry per
    ///     object with <c>"key"</c> first, which is what makes this safe; the assertion in
    ///     <see cref="Replace" /> is what makes it fail loudly if that ever stops being true.
    /// </remarks>
    static Range? Locate(string text, string key) {
        var anchor = text.IndexOf("\"key\": \"" + key + "\"", StringComparison.Ordinal);
        if (anchor < 0) {
            return null;
        }

        var next = text.IndexOf("\"key\": \"", anchor + 1, StringComparison.Ordinal);
        return anchor..(next < 0 ? text.Length : next);
    }

    static string? Field(string text, Range block, string name) {
        var slice = text[block];
        var marker = "\"" + name + "\": ";
        var start = slice.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) {
            return null;
        }

        start += marker.Length;
        if (slice[start] != '"') {
            // `null`, or a number the importer wrote unquoted.
            var end = slice.IndexOfAny([',', '\n'], start);
            return end < 0 ? null : slice[start..end].Trim();
        }

        var close = slice.IndexOf('"', start + 1);
        return close < 0 ? null : slice[(start + 1)..close];
    }

    static string Replace(string text, Range block, string name, string value) {
        var (offset, length) = block.GetOffsetAndLength(text.Length);
        var slice = text.Substring(offset, length);
        var marker = "\"" + name + "\": ";
        var start = slice.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) {
            throw new InvalidOperationException($"the registry entry has no \"{name}\" field to patch.");
        }

        start += marker.Length;
        var end = slice[start] == '"'
            ? slice.IndexOf('"', start + 1) + 1
            : slice.IndexOfAny([',', '\n'], start);
        if (end <= start) {
            throw new InvalidOperationException($"the registry entry's \"{name}\" field did not terminate.");
        }

        return text[..(offset + start)]
            + "\"" + value + "\""
            + text[(offset + end)..];
    }
}
