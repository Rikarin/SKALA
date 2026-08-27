using System.Globalization;
using Rikarin.Skala.Testing;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
/// One <c>cleanupcode</c> invocation over a directory per fixture, each with its own configuration.
/// </summary>
/// <remarks>
/// ⚠ This shape, and not a shared <c>.editorconfig</c>, is what makes a batched run answer a
/// question about one option rather than about a configuration. Batching by value index is the only
/// affordable arrangement — <c>cleanupcode</c>'s startup is tens of seconds and ~950 configurations
/// one at a time is not viable — but with one config for the whole batch every fixture is moved by
/// every other option in it. M3's first attempt at exactly this came back "197 options set, 0
/// fixtures unchanged". A directory per fixture, each carrying its own <c>root = true</c> and its
/// own single override, gives the batching for free and the isolation with it.
/// <para>
/// ⚠ The result is index-aligned with the batch, and a slot is <see langword="null"/> when
/// <c>cleanupcode</c> produced nothing for it. A missing output is a hole in the measurement;
/// callers must not score it as agreement.
/// </para>
/// </remarks>
public static class ScratchTree {
    public static string?[] Format(
        OracleRunner runner,
        IReadOnlyList<SweepCandidate> batch,
        Func<SweepCandidate, string> config
    ) {
        var scratch = Directory.CreateTempSubdirectory("skala-sweep-");
        try {
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), OracleRunner.ProjectFile);
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), OracleRunner.SolutionFile);

            var produced = new string[batch.Count];
            for (var i = 0; i < batch.Count; i++) {
                var directory = Path.Combine(scratch.FullName, "d" + i.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, ".editorconfig"), config(batch[i]));
                produced[i] = Path.Combine(directory, "F.cs");
                File.Copy(batch[i].Fixture.Path, produced[i]);
            }

            var bodies = runner.FormatInPlace(scratch.FullName, produced, OracleProfile.FormatOnly);
            var results = new string?[batch.Count];
            for (var i = 0; i < batch.Count; i++) {
                results[i] = bodies.TryGetValue(produced[i], out var body)
                    ? TextNormalisation.Normalise(body)
                    : null;
            }

            return results;
        } finally {
            try {
                scratch.Delete(recursive: true);
            } catch (IOException) {
                // A scratch directory the tool still holds open is not worth failing a sweep over.
            }
        }
    }
}
