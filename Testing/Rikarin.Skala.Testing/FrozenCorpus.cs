using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rikarin.Skala.Testing;

/// <summary>One key forced to one value, in the shape an override list can hold more than one of.</summary>
/// <remarks>
///     ⚠ A list rather than a <c>Key</c>/<c>Value</c> pair on the row, and that is the extension point
///     rather than ceremony. <c>PairwiseSweep</c> already forces two keys at once, and the width
///     capture being built alongside this one forces a wrap key together with
///     <c>max_line_length</c>. A row shaped as one key cannot hold either without a schema change that
///     would invalidate every committed row; a row shaped as a list holds both today.
/// </remarks>
public sealed record FrozenOverride(string Key, string Value);

/// <summary>
///     One distinct output the oracle produced, and where its bytes are committed.
/// </summary>
/// <param name="Path">Relative to <see cref="FrozenCorpus.Root" />, forward-slashed.</param>
/// <param name="Fixture">The corpus file, as <c>conformance-sweep.json</c> spells it.</param>
/// <param name="Profile">The <c>cleanupcode</c> profile the output was produced under.</param>
/// <param name="OracleHash">The digest the committed sweep recorded for it. The file is named by it.</param>
/// <param name="Bytes">The frozen body's length, header excluded.</param>
/// <param name="Origin">
///     ⚠ How the bytes were obtained: <see cref="FrozenCorpus.Reproduced" /> when Skala reproduced the
///     oracle's recorded digest and the bytes were taken from Skala, or
///     <see cref="FrozenCorpus.Measured" /> when only <c>jb cleanupcode</c> could produce them. It is
///     recorded because the two are not equally strong evidence and a reader must be able to tell them
///     apart — see <c>FrozenFreeze</c>.
/// </param>
public sealed record FrozenOutput(
    string Path,
    string Fixture,
    string Profile,
    string OracleHash,
    int Bytes,
    string Origin);

/// <summary>
///     One configuration the sweep measured, and the frozen output it must reproduce.
/// </summary>
/// <param name="Overrides">The keys forced, in the order they are applied.</param>
/// <param name="Fixture">The corpus file formatted under them.</param>
/// <param name="Output">The <see cref="FrozenOutput.Path" /> the oracle produced here.</param>
/// <param name="OracleHash">The oracle's digest, duplicated from the output so a row reads on its own.</param>
/// <param name="SkalaHash">Skala's digest when the sweep measured it.</param>
/// <param name="Outcome">The sweep's verdict for the option this row belongs to.</param>
/// <param name="Expectation">
///     ⚠ <see cref="FrozenCorpus.Reproduces" /> or <see cref="FrozenCorpus.Divergent" />, and the
///     distinction is the whole reason this corpus is safe to keep after the oracle is gone. A
///     <see cref="FrozenCorpus.Divergent" /> row's frozen file holds the <em>oracle's</em> answer and
///     Skala is known not to produce it; freezing Skala's answer there would make a known-wrong output
///     the permanent standard, with nothing left to appeal to.
/// </param>
/// <param name="Divergence">The <c>docs/divergences.md</c> entry that argues the disagreement.</param>
/// <param name="Source">Which committed measurement this row was frozen from.</param>
public sealed record FrozenConfiguration(
    IReadOnlyList<FrozenOverride> Overrides,
    string Fixture,
    string Output,
    string OracleHash,
    string SkalaHash,
    string Outcome,
    string Expectation,
    string? Divergence,
    string Source);

/// <summary>
///     What this corpus is a claim about: one ReSharper, one configuration, one commit.
/// </summary>
/// <remarks>
///     ⚠ The same three facts <c>ProvenanceTests</c> already enforces for the <c>.expected.cs</c>
///     fixtures and for <c>conformance-sweep.md</c>, in the same spellings, because this artefact
///     outlives the tool that justified it. A frozen corpus that does not say what it froze is a
///     folder of bytes.
/// </remarks>
/// <param name="ReSharperVersion">The oracle the sweep was measured against.</param>
/// <param name="ConfigDigest">
///     <see cref="OracleFixture.HashConfig" /> of the base <c>.editorconfig</c>. ⚠ The freeze refuses to
///     run when this has moved, rather than silently freezing a different configuration's outputs.
/// </param>
/// <param name="Commit">The tree the freeze ran on.</param>
/// <param name="Frozen">The day it ran.</param>
/// <param name="Sweep">The committed measurement whose hashes authorised every byte written.</param>
public sealed record FrozenProvenance(
    string ReSharperVersion,
    string ConfigDigest,
    string Commit,
    string Frozen,
    string Sweep);

/// <summary>The index beside the frozen bytes.</summary>
public sealed record FrozenManifest(
    FrozenProvenance Provenance,
    IReadOnlyList<FrozenOutput> Outputs,
    IReadOnlyList<FrozenConfiguration> Configurations);

/// <summary>
///     The key-flip sweep's per-configuration outputs, committed, so that the guarantee they carry
///     survives ReSharper's uninstallation.
/// </summary>
/// <remarks>
///     ⚠ <b>The hole this closes.</b> There are two conformance guarantees in this repository and only
///     one of them is standalone. The <c>.expected.cs</c> fixtures pin Skala at the export's values and
///     need nothing but the committed bytes. The key-flip sweep pins Skala at <em>every other</em>
///     value — and it commits verdicts, throwing the bytes away. The day <c>jb</c> is uninstalled that
///     second guarantee evaporates: a regression at <c>indent_style = tab</c> would leave every
///     instrument in the repository green.
///     <para>
///         ⚠ <b>Deduplicated by output digest, not stored per configuration.</b> 850 configurations
///         produce 682 distinct (fixture, output) pairs, because most of an option's values produce the
///         output some other value already produced. Storing 850 files would be 25 % waste and, worse,
///         a diff nobody can read.
///     </para>
///     <para>
///         ⚠ <b>Under <c>Testing/corpus/</c> and named <c>*.expected.cs</c> deliberately.</b> That is
///         what puts these files inside <see cref="Corpus.Fixtures" />, which is what makes
///         <c>ProvenanceTests.EveryFixture_RecordsTheConfigurationInForce</c> and
///         <c>EveryFixture_RecordsTheSameReSharperVersion</c> police them for free — and the suffix is
///         what keeps <see cref="Corpus.Files" /> and <c>CorpusSample.IsExcluded</c> from mistaking one
///         for a corpus input, on exactly the argument <see cref="OracleProfile" /> already records for
///         its own two suffixes.
///     </para>
/// </remarks>
public static class FrozenCorpus {
    /// <summary>The corpus set the frozen outputs live in.</summary>
    public const string Set = "sweep";

    /// <summary>The manifest's file name, beside the bytes it indexes.</summary>
    public const string ManifestName = "manifest.json";

    /// <summary>Skala reproduced the oracle's recorded digest, and the bytes are Skala's.</summary>
    public const string Reproduced = "skala-reproduced";

    /// <summary>Only <c>jb cleanupcode</c> could produce these bytes, and they are the oracle's.</summary>
    public const string Measured = "oracle-measured";

    /// <summary>Skala must reproduce the frozen output byte for byte.</summary>
    public const string Reproduces = "reproduces";

    /// <summary>⚠ Skala is known <em>not</em> to produce it, and an argued entry says why.</summary>
    public const string Divergent = "divergent";

    static readonly JsonSerializerOptions Options = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary><c>Testing/corpus/sweep/</c>.</summary>
    public static string Root => Path.Combine(Corpus.Root, Set);

    public static string ManifestPath => Path.Combine(Root, ManifestName);

    /// <summary>
    ///     Where one distinct output is committed: a directory per swept fixture, a file per digest.
    /// </summary>
    /// <remarks>
    ///     ⚠ Named by the digest and not by the value that produced it, because the value is not the
    ///     identity — fifteen values of <c>csharp_new_line_before_open_brace</c> produce five outputs,
    ///     and a file per value would commit the same bytes ten times over and make an added value look
    ///     like an added output. The manifest carries the values; the file name carries the identity.
    /// </remarks>
    public static string PathFor(string fixture, string oracleHash) =>
        fixture[..^".cs".Length] + "/" + oracleHash + ".expected.cs";

    /// <summary>The frozen body, with the provenance header line removed.</summary>
    /// <remarks>
    ///     ⚠ Byte-exact after the first newline, and never normalised.
    ///     <c>resharper_enforce_line_ending_style</c> and <c>resharper_csharp_insert_final_newline</c>
    ///     are two of the options frozen here and normalisation erases their entire effect — the sweep
    ///     records that it had to read their verdicts off the raw bytes for the same reason.
    /// </remarks>
    public static string ReadBody(string path) {
        var text = File.ReadAllText(path);
        var newLine = text.IndexOf('\n', StringComparison.Ordinal);
        return newLine >= 0 && text.StartsWith(OracleHeader.Prefix, StringComparison.Ordinal)
            ? text[(newLine + 1)..]
            : text;
    }

    public static void WriteBody(string path, string body, OracleHeader header) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, header + "\n" + body);
    }

    public static FrozenManifest? ReadManifest(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<FrozenManifest>(File.ReadAllText(path), Options) : null;

    public static void WriteManifest(string path, FrozenManifest manifest) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, Options) + "\n");
    }

    /// <summary>Every frozen body on disk, so an orphan is visible rather than merely unused.</summary>
    public static IReadOnlyList<string> Bodies() =>
        Directory.Exists(Root)
            ? [
                .. Directory.EnumerateFiles(Root, "*.expected.cs", SearchOption.AllDirectories)
                    .Select(static path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
                    .OrderBy(static path => path, StringComparer.Ordinal)
            ]
            : [];
}
