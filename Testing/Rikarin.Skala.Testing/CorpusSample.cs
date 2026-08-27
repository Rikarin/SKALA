using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rikarin.Skala.Testing;

/// <summary>
/// Draws the <c>corpus/real/vixen/</c> sample from a tree, reproducibly.
/// </summary>
/// <remarks>
/// ⚠ Milestone 3.1 found that 167 of the 200 files in that directory had been vendored from
/// <c>.claude/worktrees/</c> — agent scratch checkouts — rather than from the mainline tree. The
/// content was real and the numbers stood, but the provenance was unrecorded and unrepeatable, and
/// Vixen is over half the fidelity weight. This is the repair: a sampler that is part of the
/// repository, so "which 200 files" has an answer that survives the person who ran it.
/// <para>
/// ⚠ The selection is a hash of the path rather than a seeded pseudo-random sequence. A PRNG's
/// answer depends on the order the file system happened to enumerate in and on how many candidates
/// were rejected before it; a hash of the path depends on nothing but the path, so the same commit
/// and the same filters give the same 200 files on any machine, in any order, forever.
/// </para>
/// </remarks>
public static class CorpusSample {
    /// <summary>The sample's seed, mixed into every path's hash.</summary>
    public const string Seed = "skala-corpus-20260826";

    /// <summary>Files shorter or longer than this are not sampled.</summary>
    const int MinimumLines = 40;

    const int MaximumLines = 900;

    /// <summary>A candidate file and the key it sorts by.</summary>
    public sealed record Candidate(string RelativePath, string FullPath, ulong Key, int Lines);

    /// <summary>Every file of <paramref name="root"/> the sample may draw from, in key order.</summary>
    public static List<Candidate> Candidates(string root, string seed = Seed) {
        var candidates = new List<Candidate>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (IsExcluded(relative)) {
                continue;
            }

            var lines = File.ReadAllLines(path).Length;
            if (lines is < MinimumLines or > MaximumLines) {
                continue;
            }

            candidates.Add(new Candidate(relative, path, KeyOf(seed, relative), lines));
        }

        candidates.Sort(static (left, right) => left.Key != right.Key
                ? left.Key.CompareTo(right.Key)
                : string.CompareOrdinal(left.RelativePath, right.RelativePath)
        );

        return candidates;
    }

    /// <summary>
    /// What is never sampled, and why.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>.claude/</c> is first on the list and it is the whole point of this file: an agent's
    /// worktree is a copy of the tree, so sampling it both duplicates content and records a
    /// provenance that will not exist next week. Build output and generated sources are excluded
    /// because a formatter's agreement with the oracle over a generated file measures the generator.
    /// </remarks>
    static bool IsExcluded(string relative) {
        foreach (var segment in relative.Split('/')) {
            if (segment is ".claude" or "bin" or "obj" or "artifacts" or "packages"
                || segment.EndsWith(".Artifacts", StringComparison.Ordinal)) {
                return true;
            }
        }

        return relative.EndsWith(".g.cs", StringComparison.Ordinal)
            || relative.EndsWith(".generated.cs", StringComparison.Ordinal)
            || relative.EndsWith(".Designer.cs", StringComparison.Ordinal)
            || relative.EndsWith("AssemblyInfo.cs", StringComparison.Ordinal)
            || relative.EndsWith("GlobalUsings.g.cs", StringComparison.Ordinal)
            || relative.EndsWith(".expected.cs", StringComparison.Ordinal);
    }

    /// <summary>The first eight bytes of <c>SHA-256(seed + "\n" + path)</c>, big-endian.</summary>
    public static ulong KeyOf(string seed, string relativePath) {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(seed + "\n" + relativePath));
        ulong key = 0;
        for (var i = 0; i < 8; i++) {
            key = key << 8 | digest[i];
        }

        return key;
    }

    /// <summary>Copies the sample into <paramref name="destination"/>, mirroring the tree's layout.</summary>
    public static string Draw(string root, int count, string destination, TextWriter log) {
        var candidates = Candidates(root);
        var taken = candidates.Take(count).ToList();
        if (taken.Count < count) {
            log.WriteLine(
                $"only {taken.Count.ToString(CultureInfo.InvariantCulture)} candidates of the {count.ToString(CultureInfo.InvariantCulture)} asked for"
            );
        }

        if (Directory.Exists(destination)) {
            Directory.Delete(destination, recursive: true);
        }

        foreach (var file in taken) {
            var target = Path.Combine(destination, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file.FullPath, target, overwrite: true);
        }

        var areas = taken.GroupBy(static file => file.RelativePath.Split('/')[0], StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .Select(group => group.Key + " " + group.Count().ToString(CultureInfo.InvariantCulture));

        return $"{taken.Count.ToString(CultureInfo.InvariantCulture)} of {candidates.Count.ToString(CultureInfo.InvariantCulture)} candidates, "
            + $"{taken.Sum(static file => file.Lines).ToString(CultureInfo.InvariantCulture)} lines: "
            + string.Join(", ", areas);
    }
}
