using System.Text.Json;

namespace Rikarin.Skala.Release.Surfaces;

/// <summary>One option as the registry describes it.</summary>
public sealed record OptionRecord(string Key, string Tier, string Default, string Type, bool Inert);

/// <summary>
///     The option registry as a compatibility surface: which keys exist, what tier they are honoured
///     at, and what they mean.
/// </summary>
/// <remarks>
///     ⚠ The tier is the promise. docs/plan/03 and doc 12 § "The key-flip sweep": <b>Tier A</b> means
///     the option is honoured and pinned to the oracle by a sweep that measures it at every legal value;
///     <b>Tier D</b> means it is parsed and does nothing. So:
///     <list type="bullet">
///         <item>
///             <b>A → D is major.</b> A repository has a key in its <c>.editorconfig</c> that used to change its
///             files and now does not. Nothing errors, nothing warns, and the next <c>skala format</c> is a
///             repository-wide diff — the exact failure the output detector exists for, arriving through
///             configuration rather than through code.
///         </item>
///         <item>
///             <b>D → A is minor.</b> The key now does what it always said it did. That also moves files, which
///             is why it is not a patch, but nobody's stated intent has been broken.
///         </item>
///         <item>
///             <b>A changed default is major.</b> The default is what applies to every repository that has not
///             written the key down, which is most of them for most keys.
///         </item>
///         <item>
///             <b>A removed key is major</b> — <c>SK9001</c> reports an unrecognized option, and doc 09 makes
///             that a configuration error at exit 3 under <c>--strict</c>. A key that vanishes turns a green
///             <c>skala config check --strict</c> red.
///         </item>
///     </list>
///     <para>
///         ⚠ This reads <c>options.json</c> from both trees rather than re-running the conformance sweep.
///         The sweep needs JetBrains installed and takes minutes (ADR-011), so it is a nightly job and what
///         the fast path reads is the committed result — which is this file. The release measures the
///         registry the sweep last wrote; it does not re-derive it, and doc 18 says so rather than implying
///         the tier numbers were re-measured at release time.
///     </para>
/// </remarks>
public static class OptionSurface {
    public const string Name = "option registry";

    public static DetectorResult Run(string? baselineRoot, string candidateRoot) {
        var candidate = Read(candidateRoot);

        if (baselineRoot is null) {
            var tiers = candidate.Values
                .GroupBy(static option => option.Tier, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => $"{group.Count()} {group.Key}");

            return DetectorResult.Unmeasured(
                Name,
                $"no previous release — {candidate.Count} options ({string.Join(", ", tiers)}) are the baseline"
            );
        }

        var baseline = Read(baselineRoot);
        var bump = BumpKind.Patch;
        var details = new List<string>();

        foreach (var (key, before) in baseline.OrderBy(static entry => entry.Key, StringComparer.Ordinal)) {
            if (!candidate.TryGetValue(key, out var after)) {
                bump = BumpKind.Major;
                details.Add($"`{key}` was **removed** — `skala config check --strict` now exits 3 on it");
                continue;
            }

            if (!string.Equals(before.Tier, after.Tier, StringComparison.Ordinal)) {
                if (Honoured(before) && !Honoured(after)) {
                    bump = BumpKind.Major;
                    details.Add($"`{key}` **{before.Tier} → {after.Tier}** — it no longer changes anything");
                } else if (!Honoured(before) && Honoured(after)) {
                    bump = RuleSurface.Max(bump, BumpKind.Minor);
                    details.Add($"`{key}` {before.Tier} → {after.Tier} — it is honoured now");
                } else {
                    details.Add($"`{key}` {before.Tier} → {after.Tier}");
                }
            }

            // ⚠ A default or a type only breaks a promise on a key that was **honoured at the
            // baseline**. A Tier D key does nothing, so changing what it would have meant changes
            // nothing that ever happened, and calling it major makes the detector cry major on the
            // ordinary work of implementing an option. Measured: `dotnet_style_require_accessibility
            // _modifiers` moved D→A *and* changed type in the same release, and the type change was
            // part of implementing it. An unhonoured key's change is still recorded, because it is
            // the thing to look at first if the next release moves files unexpectedly.
            if (!string.Equals(before.Default, after.Default, StringComparison.Ordinal)) {
                if (Honoured(before)) {
                    bump = BumpKind.Major;
                    details.Add($"`{key}` **default changed**: `{before.Default}` → `{after.Default}`");
                } else {
                    details.Add($"`{key}` default changed while inert: `{before.Default}` → `{after.Default}`");
                }
            }

            if (!string.Equals(before.Type, after.Type, StringComparison.Ordinal)) {
                if (Honoured(before)) {
                    bump = BumpKind.Major;
                    details.Add($"`{key}` **type changed**: {before.Type} → {after.Type}");
                } else {
                    details.Add($"`{key}` type changed while inert: {before.Type} → {after.Type}");
                }
            }
        }

        var added = candidate.Keys.Where(key => !baseline.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
        if (added.Count > 0) {
            bump = RuleSurface.Max(bump, BumpKind.Minor);
            details.Add(
                $"{added.Count} option(s) added: {string.Join(", ", added.Take(10).Select(static key => $"`{key}`"))}"
                + (added.Count > 10 ? ", …" : "")
            );
        }

        var honoured = candidate.Values.Count(Honoured);
        var headline = details.Count == 0
            ? $"unchanged — {candidate.Count} options, {honoured} honoured"
            : $"{details.Count} change(s) across {candidate.Count} options ({honoured} honoured)";

        return DetectorResult.Measured(Name, bump, headline, details);
    }

    /// <summary>
    ///     Whether the option does anything. ⚠ Tier A alone; B and C are partial and D is inert, and a
    ///     promotion into a partial tier is not the promise Tier A is.
    /// </summary>
    static bool Honoured(OptionRecord option) => string.Equals(option.Tier, "A", StringComparison.Ordinal);

    public static IReadOnlyDictionary<string, OptionRecord> Read(string repositoryRoot) {
        var path = Path.Combine(repositoryRoot, "Core", "Rikarin.Skala.Options", "options.json");
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"No option registry at '{path}'.", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var options = new Dictionary<string, OptionRecord>(StringComparer.Ordinal);

        foreach (var option in document.RootElement.GetProperty("options").EnumerateArray()) {
            var key = option.GetProperty("key").GetString()!;
            options[key] = new(
                key,
                option.TryGetProperty("tier", out var tier) ? tier.GetString() ?? "" : "",
                option.TryGetProperty("default", out var value) ? Text(value) : "",
                option.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
                option.TryGetProperty("inert", out var inert) && inert.ValueKind == JsonValueKind.True
            );
        }

        if (options.Count == 0) {
            throw new InvalidOperationException($"'{path}' has no options.");
        }

        return options;
    }

    static string Text(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString() ?? "" : element.GetRawText();
}
