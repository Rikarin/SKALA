using System.Diagnostics;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Formatting.CSharp;

namespace Rikarin.Skala.Testing;

/// <summary>
///     The differential over <em>degraded</em> input, with the null hypothesis beside every number.
/// </summary>
/// <remarks>
///     ⚠ The null hypothesis is not decoration and it is not optional. <c>corpus/real/</c>'s inputs are
///     already 90.95 % line-identical to their fixtures, so a formatter that returns its input unchanged
///     scores 91 % there — and the absence of that figure beside the 99.63 % headline is what made the
///     headline look better than it was. Every number this file prints is printed next to what "change
///     nothing" scores on the same population, because the difference between them is the only part that
///     is the formatter's.
/// </remarks>
public static class UnformatDifferential {
    /// <summary>Where one mode's numbers came from.</summary>
    /// <param name="Null">Return the degraded input unchanged, scored against the oracle.</param>
    /// <param name="Bare">Skala over the degraded input, no preprocessor symbols.</param>
    /// <param name="Defined">Skala over the degraded input, with symbols supplied.</param>
    /// <param name="OracleDrift">
    ///     The oracle over the degraded input against the oracle over the <em>original</em>. ⚠ This is a
    ///     ceiling rather than a floor: where it is below 100 %, the oracle itself does not recover the
    ///     canonical form from the degraded one, and no formatter that agrees with the oracle could.
    /// </param>
    public sealed record ModeResult(
        UnformatMode Mode,
        FidelityReport Null,
        FidelityReport Bare,
        FidelityReport Defined,
        FidelityReport OracleDrift);

    /// <summary>
    ///     Measures one mode against its committed fixtures.
    /// </summary>
    /// <remarks>
    ///     ⚠ Reads files, never JetBrains (ADR-011). The degraded inputs and their fixtures are both
    ///     committed, so this runs on a machine with no ReSharper installed and its answer does not
    ///     depend on which version of the tool the person running it happens to have.
    /// </remarks>
    public static ModeResult? Measure(UnformatMode mode, IReadOnlyList<string> symbols) {
        // ⚠ Memoised. Four assertions and a report all want the same numbers, and each call formats
        // every degraded file twice; without this the conformance suite spends minutes recomputing
        // one answer.
        var key = Unformat.Name(mode) + "\u0000" + string.Join(';', symbols);
        lock (Gate) {
            if (Cache.TryGetValue(key, out var cached)) {
                return cached;
            }
        }

        var measured = Compute(mode, symbols);
        lock (Gate) {
            Cache[key] = measured;
        }

        return measured;
    }

    static readonly Dictionary<string, ModeResult?> Cache = new(StringComparer.Ordinal);
    static readonly Lock Gate = new();

    static ModeResult? Compute(UnformatMode mode, IReadOnlyList<string> symbols) {
        var files = UnformatCorpus.Files(mode).Where(static file => file.HasFixture).ToArray();
        if (files.Length == 0) {
            return null;
        }

        var nulls = new List<(string File, string Expected, string Actual)>(files.Length);
        var bare = new List<(string File, string Expected, string Actual)>(files.Length);
        var defined = new List<(string File, string Expected, string Actual)>(files.Length);
        var drift = new List<(string File, string Expected, string Actual)>(files.Length);
        var originals = OriginalsByRelativePath();

        foreach (var file in files) {
            var text = CSharpFormatter.Read(file.Path);
            var options = OptionResolver.Resolve(file.Path).Options;
            var expected = OracleFixture.Read(file);

            nulls.Add((file.ToString(), expected, text.ToString()));
            bare.Add((file.ToString(), expected, CSharpFormatter.Format(file.Path, text, options).Formatted));
            defined.Add(
                (file.ToString(), expected, CSharpFormatter.Format(file.Path, text, options, null, symbols).Formatted)
            );

            // ⚠ Keyed by the path *below* the mode directory, which is the corpus/real/ relative
            // path the degraded copy was made from. Losing that pairing would silently compare a
            // file with a different file and report it as a divergence class.
            var source = SourceOf(file, mode);
            if (originals.TryGetValue(source, out var original) && original.HasFixture) {
                drift.Add((file.ToString(), OracleFixture.Read(original), expected));
            }
        }

        return new ModeResult(
            mode,
            Fidelity.Compare(nulls),
            Fidelity.Compare(bare),
            Fidelity.Compare(defined),
            Fidelity.Compare(drift)
        );
    }

    static string SourceOf(CorpusFile file, UnformatMode mode) => file.RelativePath[(Unformat.Name(mode).Length + 1)..];

    static Dictionary<string, CorpusFile> OriginalsByRelativePath() =>
        Corpus.Files(Corpus.Real).ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);

    /// <summary>
    ///     <c>corpus/real/</c>'s own null hypothesis: its inputs scored directly against its fixtures.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured here, through <see cref="Fidelity.Compare" />, rather than quoted from anywhere.
    ///     It is the calibration for the number the whole project is steered by, and a calibration that
    ///     came from a different diff basis than the number it calibrates is worse than none — the
    ///     LCS/positional gap on this corpus is forty points (docs/plan/12 § 2). Printed at the top of
    ///     every unformat report so the two floors are read side by side.
    /// </remarks>
    public static FidelityReport RealCorpusNullHypothesis() {
        var results = new List<(string File, string Expected, string Actual)>();
        foreach (var file in Corpus.Files(Corpus.Real).Where(static file => file.HasFixture)) {
            results.Add((file.ToString(), OracleFixture.Read(file), CSharpFormatter.Read(file.Path).ToString()));
        }

        return Fidelity.Compare(results);
    }

    /// <summary>The whole report, both modes, with the ranked divergence classes behind each.</summary>
    public static string Render(IReadOnlyList<string> symbols, int topClasses = 14, int topConstructs = 14) {
        var builder = new StringBuilder();
        builder.AppendLine("                              line      file      lines");
        Row(builder, "corpus/real null", RealCorpusNullHypothesis());
        builder.AppendLine("  ⚠ what the existing differential's 99.63 % sits on: change nothing, score this.");
        builder.AppendLine();
        foreach (var mode in Unformat.Modes) {
            var result = Measure(mode, symbols);
            if (result is null) {
                builder.Append("── ")
                    .Append(Unformat.Name(mode))
                    .AppendLine(" ── no fixtures. `unformat regenerate` (needs jb).");
                continue;
            }

            builder.Append("── ").Append(Unformat.Name(mode)).AppendLine(" ──────────────────────────────────────");
            builder.AppendLine("                              line      file      lines");
            Row(builder, "null hypothesis", result.Null);
            Row(builder, "skala, no symbols", result.Bare);
            Row(builder, "skala, with symbols", result.Defined);
            Row(builder, "oracle vs original", result.OracleDrift);
            builder.AppendLine();

            // ⚠ The share of the *available* gap, which is the only honest way to compare a number
            // taken over a 91 % floor with one taken over a floor near zero. 99.63 % over a 90.95 %
            // null closes 95.9 % of the gap; the same 99.63 % over a 4 % null would close 99.6 %,
            // and the two are not the same achievement.
            builder.Append("  gap closed by Skala: line ")
                .Append(Share(result.Null.LineFidelity, result.Bare.LineFidelity))
                .Append(", file ")
                .AppendLine(Share(result.Null.FileFidelity, result.Bare.FileFidelity));
            builder.AppendLine();

            builder.AppendLine("divergence classes, by line count (Skala vs the oracle, symbols supplied):");
            builder.AppendLine(result.Defined.Render(topClasses));

            builder.AppendLine("constructs, by divergent lines:");
            builder.Append(Constructs(UnformatCorpus.Set + "/" + Unformat.Name(mode), symbols, topConstructs));

            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    ///     The construct attribution docs/plan/16 § R1 asks for, over the degraded corpus.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ConstructReport</c> is reused as-is by naming the mode subdirectory as the set, rather
    ///     than copied. Its attribution — every divergent line charged to the innermost node of the
    ///     <em>oracle's</em> output that owns it — is exactly the question here, and a second
    ///     implementation of it would be a second answer to "which construct is this line".
    /// </remarks>
    static string Constructs(string set, IReadOnlyList<string> symbols, int top) {
        var builder = new StringBuilder();
        foreach (var share in ConstructReport.Build(set, symbols).Where(static s => s.Divergent > 0).Take(top)) {
            builder.Append("  ")
                .Append(share.Divergent.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append(" / ")
                .Append(share.Lines.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append(" lines  ")
                .Append((share.Fidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(6))
                .Append(" %  ")
                .Append(share.Occurrences.ToString(CultureInfo.InvariantCulture).PadLeft(6))
                .Append(" occurrences  ")
                .AppendLine(share.Kind);
        }

        return builder.ToString();
    }

    static string Share(double floor, double reached) =>
        floor >= 1
            ? "n/a"
            : ((reached - floor) / (1 - floor) * 100).ToString("F1", CultureInfo.InvariantCulture) + " %";

    static void Row(StringBuilder builder, string label, FidelityReport report) =>
        builder.Append("  ")
            .Append(label.PadRight(24))
            .Append((report.LineFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .Append(" % ")
            .Append((report.FileFidelity * 100).ToString("F2", CultureInfo.InvariantCulture).PadLeft(7))
            .Append(" %   (")
            .Append(report.IdenticalLines.ToString(CultureInfo.InvariantCulture))
            .Append('/')
            .Append(report.Lines.ToString(CultureInfo.InvariantCulture))
            .AppendLine(")");

    // ── regeneration ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Runs <c>jb cleanupcode</c> over the committed degraded inputs and writes the fixtures.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only <c>unformat regenerate</c> and <c>unformat oracle</c> call this, and both are
    ///     deliberate reviewed actions (ADR-011). ⚠ Oracle runs dominate the cost of this whole
    ///     exercise — <c>cleanupcode</c>'s startup is tens of seconds and its per-file marginal cost is
    ///     milliseconds — so the batch is as large as the tool will hold rather than as small as is
    ///     tidy. The wall-clock cost per batch is printed, because "budget it and report the real cost"
    ///     is only possible if somebody measured it.
    /// </remarks>
    public static int Regenerate(OracleRunner runner, string editorConfig, TextWriter log) {
        var version = runner.Version;
        var hash = OracleFixture.HashConfig(editorConfig);
        var header = new OracleHeader(version, hash, OracleRunner.Profile, OracleFixture.Today);
        log.WriteLine($"oracle: resharper={version} config=sha256:{hash} profile={OracleRunner.Profile}");

        var written = 0;
        foreach (var mode in Unformat.Modes) {
            var files = UnformatCorpus.Files(mode);
            if (files.Count == 0) {
                continue;
            }

            const int batch = 60;
            for (var start = 0; start < files.Count; start += batch) {
                var slice = files.Skip(start).Take(batch).ToArray();
                var clock = Stopwatch.StartNew();
                var results = runner.Format(slice, editorConfig);
                clock.Stop();

                foreach (var file in slice) {
                    if (results.TryGetValue(file.Path, out var body)) {
                        OracleFixture.Write(file, body, header);
                        written++;
                    }
                }

                log.WriteLine(
                    $"  {Unformat.Name(mode)}: "
                    + $"{Math.Min(start + batch, files.Count).ToString(CultureInfo.InvariantCulture)}"
                    + $"/{files.Count.ToString(CultureInfo.InvariantCulture)}"
                    + $"  {clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)} s"
                    + $"  ({(clock.Elapsed.TotalSeconds / Math.Max(1, slice.Length)).ToString("F2", CultureInfo.InvariantCulture)} s/file)"
                );
            }
        }

        return written;
    }
}
