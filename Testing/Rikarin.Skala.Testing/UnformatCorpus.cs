using System.Globalization;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
/// <c>Testing/corpus/unformatted/</c> — the third corpus, and the second differential.
/// </summary>
/// <remarks>
/// ⚠ The reason it exists is a number. Comparing each <c>corpus/real/</c> <b>input</b> directly
/// against its <c>.expected.cs</c> — scoring a formatter that returns its input unchanged — gives
/// 92.08 % of lines and 26.84 % of files. The 99.63 % headline therefore sits on a 92 % floor: 92 %
/// of corpus lines never needed changing, so the whole discriminating power of that differential
/// lives in the other 8 %, and the test mostly asks <em>"does Skala leave good code alone"</em>
/// rather than <em>"does Skala make the same decisions ReSharper makes"</em>. The second question is
/// the one that decides whether ReSharper can be retired.
/// <para>
/// ⚠ The degraded inputs are <b>committed</b>, like every other oracle input, and so are the
/// fixtures beside them (ADR-011). A degraded input regenerated on the fly is an input nobody
/// reviewed, and a fixture regenerated beside it is a tautology.
/// </para>
/// </remarks>
public static class UnformatCorpus {
    public const string Set = "unformatted";

    /// <summary>
    /// The seed the sample is drawn with, mixed into every path's hash.
    /// </summary>
    /// <remarks>
    /// ⚠ Its own seed rather than <see cref="CorpusSample.Seed"/>, so that redrawing this sample and
    /// redrawing <c>corpus/real/vixen/</c> are independent actions. Sharing one seed would make a
    /// Vixen redraw silently reshuffle this corpus too.
    /// </remarks>
    public const string Seed = "skala-unformat-20260827";

    /// <summary>
    /// How many of <c>corpus/real/</c>'s 380 files each mode is drawn over.
    /// </summary>
    /// <remarks>
    /// ⚠ A measured subset rather than the whole set, and the arithmetic is in
    /// docs/plan/12 § "The unformat differential". Two modes over 380 files is 760 oracle files and
    /// about 90 MB of committed fixtures; 120 files is 240 oracle files in four
    /// <c>jb cleanupcode</c> invocations and about 28 MB, over roughly 24 000 degraded lines — which
    /// is far more line evidence than the 8 % of <c>corpus/real/</c> that the existing differential
    /// actually discriminates on. A subset that is measured beats a full run that never completes.
    /// </remarks>
    public const int SampleSize = 120;

    public static string Root { get; } = Corpus.SetRoot(Set);

    public static string ModeRoot(UnformatMode mode) => Path.Combine(Root, Unformat.Name(mode));

    /// <summary>The <c>corpus/real/</c> files the sample draws from, in hash order.</summary>
    /// <remarks>
    /// ⚠ Ordered by <c>SHA-256(seed + "\n" + path)</c> for <see cref="CorpusSample"/>'s reason: a
    /// hash of the path depends on nothing but the path, so the same commit gives the same files on
    /// any machine, in any order, forever — while a seeded sequence depends on the order the file
    /// system happened to enumerate in.
    /// </remarks>
    public static IReadOnlyList<CorpusFile> Sources(int count) => [
        .. Corpus.Files(Corpus.Real)
            .OrderBy(static file => CorpusSample.KeyOf(Seed, file.RelativePath))
            .ThenBy(static file => file.RelativePath, StringComparer.Ordinal)
            .Take(count)
    ];

    /// <summary>The degraded files of one mode that are on disk, fixture or not.</summary>
    public static IReadOnlyList<CorpusFile> Files(UnformatMode mode) {
        var root = ModeRoot(mode);
        if (!Directory.Exists(root)) {
            return [];
        }

        return [
            .. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !path.EndsWith(".expected.cs", StringComparison.Ordinal))
                .Select(path => new CorpusFile(
                        Set,
                        Unformat.Name(mode) + "/" + Path.GetRelativePath(root, path).Replace('\\', '/'),
                        path
                    )
                )
                .OrderBy(static file => file.Path, StringComparer.Ordinal)
        ];
    }

    /// <summary>
    /// Degrades the sample and writes it, replacing whatever was there.
    /// </summary>
    /// <remarks>
    /// ⚠ A deliberate developer action whose diff is reviewed in its own commit, exactly like
    /// <c>sample</c> and <c>oracle</c>. It replaces a corpus, and a corpus that changes without a
    /// commit is not a measurement.
    /// </remarks>
    public static string Generate(int count, TextWriter log) {
        var sources = Sources(count);
        var report = new StringBuilder();

        foreach (var mode in Unformat.Modes) {
            var root = ModeRoot(mode);
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }

            var written = 0;
            var rejected = new List<string>();
            var glued = 0;
            long originalLines = 0;
            long degradedLines = 0;

            foreach (var source in sources) {
                var text = File.ReadAllText(source.Path);
                var seed = CorpusSample.KeyOf(Seed + "/" + Unformat.Name(mode), source.RelativePath);
                var degraded = Unformat.Degrade(mode, text, seed);
                if (degraded is null) {
                    // ⚠ Reported rather than silently dropped. A file the degrader cannot prove it
                    // preserved is a hole in the corpus, and a hole nobody counted is how a
                    // measurement quietly narrows to the easy files.
                    rejected.Add(source.RelativePath);
                    continue;
                }

                var target = Path.Combine(root, source.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, degraded.Text);
                written++;
                glued += degraded.Glued ? 1 : 0;
                originalLines += TextNormalisation.Lines(text).Length;
                degradedLines += degraded.Lines;
            }

            report.Append(Unformat.Name(mode))
                .Append(": ")
                .Append(written.ToString(CultureInfo.InvariantCulture))
                .Append(" files, ")
                .Append(originalLines.ToString(CultureInfo.InvariantCulture))
                .Append(" lines → ")
                .Append(degradedLines.ToString(CultureInfo.InvariantCulture))
                .Append(" lines (")
                .Append((originalLines == 0 ? 0 : (double)degradedLines / originalLines)
                    .ToString("P1", CultureInfo.InvariantCulture)
                )
                .AppendLine(")");

            if (glued > 0) {
                report.Append("  ")
                    .Append(glued.ToString(CultureInfo.InvariantCulture))
                    .AppendLine(" files fell back to a space between every token pair");
            }

            if (rejected.Count > 0) {
                report.Append("  ⚠ ")
                    .Append(rejected.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" rejected (not provably the same program): ")
                    .AppendLine(string.Join(", ", rejected.Take(6)));
            }

            log.WriteLine($"  {Unformat.Name(mode)}: {written.ToString(CultureInfo.InvariantCulture)} written");
        }

        WriteNotice(sources.Count, report.ToString());
        return report.ToString();
    }

    static void WriteNotice(int count, string report) {
        Directory.CreateDirectory(Root);
        File.WriteAllText(
            Path.Combine(Root, "NOTICE.md"),
            $"""
             # `corpus/unformatted/`

             Degraded copies of the first {count.ToString(CultureInfo.InvariantCulture)} files of
             `corpus/real/` in `SHA-256("{Seed}" + "\n" + path)` order, one subtree per degradation
             mode, each with the `jb cleanupcode` output of the **degraded** file beside it.

             ⚠ Generated by `dotnet run --project Testing/Rikarin.Skala.Testing -- unformat regenerate`,
             which is a deliberate developer action like `oracle` and `sample`. Do not hand-edit; the
             inputs and the fixtures only mean anything as a pair.

             ⚠ These files are inputs. `./build.sh Lint` excludes `Testing/corpus` for exactly this
             reason — half the corpus is deliberately misformatted, and a formatter that reformats
             its own test corpus has destroyed its own measurement.

             ```
             {report.TrimEnd().Replace("\n", "\n             ", StringComparison.Ordinal)}
             ```

             `./build.sh Unformat` reports the differential, with the null hypothesis beside every
             number. See docs/plan/12 § "The unformat differential".
             """
        );
    }
}
