using Rikarin.Skala.Options;
using System.Collections.Frozen;

namespace Rikarin.Skala.Core.Configuration;

/// <summary>
///     The bridge between ReSharper's key namespace and Skala's, in the one direction that still
///     exists.
/// </summary>
/// <remarks>
///     ⚠ <b>This is provenance, not ingestion, and the distinction is the whole point.</b> Skala's
///     option keys are <c>skala_*</c>; a <c>resharper_*</c> key in a user's <c>.editorconfig</c> is an
///     unknown key and reports <c>SK9001</c>. Nothing here is reachable from
///     <see cref="OptionRegistry.TryResolve" />, and it must stay that way — the moment an export
///     spelling resolves through the registry, pointing Skala at a Rider export silently configures it
///     again and the rename has been undone without a test noticing.
///     <para>
///         What it is for is the three places that must still read the export as an <em>artefact</em>:
///         <see cref="CanonicalEditorConfig.Compose" />, which translates it into the payload shipped
///         to consuming repositories; <c>OracleRunner</c>, which has to speak ReSharper's spelling to
///         <c>jb cleanupcode</c> because that is the only namespace it understands; and
///         <c>EditorConfigIngestionTests</c>, which states that Skala's own configuration says what the
///         export says. ADR-001's workflow — change a setting in Rider, re-export, publish — is intact;
///         only the spelling on the consumer's side of it has changed.
///     </para>
/// </remarks>
public static class ExportSpellings {
    static readonly FrozenDictionary<string, OptionId> ByExportSpelling = Build();

    /// <summary>The option the export spells <paramref name="spelling" />, if Skala knows it.</summary>
    public static bool TryResolve(string spelling, out OptionId id) => ByExportSpelling.TryGetValue(spelling, out id);

    /// <summary>
    ///     The spelling <c>jb cleanupcode</c> understands for <paramref name="id" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ The most specific one, matching the winner <see cref="OptionResolver" /> would pick, because
    ///     an .editorconfig's more specific key beats its more generic one inside ReSharper too. Handing
    ///     the oracle the generic spelling of an option whose specific spelling the export also sets
    ///     would leave the export's value in force and measure nothing.
    /// </remarks>
    public static string ForOracle(OptionId id) {
        var info = OptionRegistry.Get(id);
        return info.Export.Count == 0 ? info.Key : info.Export[0];
    }

    static FrozenDictionary<string, OptionId> Build() {
        var map = new Dictionary<string, OptionId>(StringComparer.Ordinal);
        foreach (var info in OptionRegistry.All) {
            foreach (var spelling in info.Export) {
                // ⚠ First wins, deterministically: the registry is ordered and an export spelling that
                // reached two options would be SK9004 at generation time, so this cannot silently
                // pick between two live options.
                map.TryAdd(spelling, info.Id);
            }
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
