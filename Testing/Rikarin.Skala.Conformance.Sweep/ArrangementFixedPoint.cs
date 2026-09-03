using Rikarin.Skala.Formatting.CSharp.Arrangement;
using Rikarin.Skala.Testing;
using System.Globalization;

namespace Rikarin.Skala.Conformance.Sweep;

/// <summary>
///     Whether the two sides of an arrangement verdict are both standing still.
/// </summary>
/// <remarks>
///     ⚠ <b>The measurement bug this rules out.</b> Skala's half of a cleanup-profile comparison is
///     <see cref="ArrangementPipeline" />, which <em>loops</em>: a rewrite can expose a rewrite that was
///     not available before it, and <c>ArrangementPropertyTests.Convergence_HoldsWithinTheBound</c>
///     records three passes as the observed maximum over the corpus. The oracle's half is one
///     <c>cleanupcode</c> invocation. If <c>cleanupcode</c> were a single pass over a problem that needs
///     two, every fixture where it matters would show Skala having gone further — and the sweep would
///     report that as the flipped key diverging, when it is the two sides having been stopped at
///     different places.
///     <para>
///         ⚠ It cannot be settled by reading: <c>cleanupcode</c>'s own pass structure is not documented and
///         is not this repository's to assume. So it is asked. The oracle rewrites in place, which makes the
///         experiment exact rather than approximate — run it over the subtree, then run it again over what
///         it just wrote, and any file that moves the second time is a file the first invocation left short
///         of a fixed point.
///     </para>
///     <para>
///         ⚠ The negative control is the whole point and it is why this reports the *first* pass's change
///         count too. "Nothing moved on the second pass" is worthless if nothing moved on the first either:
///         that is a tool that did not run, and it is the same shape as
///         <see cref="KeyFlipSweep.IsUnvaryingRound" />. Both counts are printed; read them together.
///     </para>
/// </remarks>
public static class ArrangementFixedPoint {
    public static int Run(string baseConfigPath, TextWriter log) {
        if (OracleRunner.FindExecutableOrNull() is null) {
            log.WriteLine("jb (JetBrains.Skala.GlobalTools) is not installed; this asks the oracle a question.");
            return 3;
        }

        var runner = new OracleRunner();
        var config = File.ReadAllText(OracleEditorConfig.Reading(baseConfigPath));
        var files = Corpus.ArrangementConstructs();
        log.WriteLine(
            $"arrangement fixed point: {Count(files.Count)} files under {Corpus.Constructs}/{Corpus.ArrangementPrefix}"
        );

        var scratch = Directory.CreateTempSubdirectory("skala-fixedpoint-");
        try {
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.csproj"), OracleRunner.ProjectFile);
            File.WriteAllText(Path.Combine(scratch.FullName, "Oracle.sln"), OracleRunner.SolutionFile);

            var copies = new string[files.Count];
            var original = new string[files.Count];
            for (var i = 0; i < files.Count; i++) {
                var directory = Path.Combine(scratch.FullName, "d" + i.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, ".editorconfig"), config);
                copies[i] = Path.Combine(directory, "F.cs");
                File.Copy(files[i].Path, copies[i]);
                original[i] = File.ReadAllText(files[i].Path);
            }

            // ⚠ Twice over the same directory. `FormatInPlace` rewrites the files it is given, so the
            // second call's input is exactly the first call's output — no re-copying, and therefore no
            // chance of the second run seeing a different project from the first.
            var first = runner.FormatInPlace(scratch.FullName, copies, OracleProfile.Cleanup);
            var second = runner.FormatInPlace(scratch.FullName, copies, OracleProfile.Cleanup);

            var movedFirst = 0;
            var movedSecond = 0;
            var answered = 0;
            var unstable = new List<string>();

            for (var i = 0; i < files.Count; i++) {
                if (!first.TryGetValue(copies[i], out var one) || !second.TryGetValue(copies[i], out var two)) {
                    continue;
                }

                answered++;
                if (!Same(original[i], one)) {
                    movedFirst++;
                }

                if (!Same(one, two)) {
                    movedSecond++;

                    // ⚠ With the line that moved, not just the file name. "The oracle is not
                    // idempotent here" is a claim somebody has to be able to check without
                    // re-running the tool, and which key's verdict it qualifies depends entirely on
                    // *what* moved.
                    unstable.Add(files[i] + Difference(one, two));
                }
            }

            log.WriteLine($"  answered:              {Count(answered)}/{Count(files.Count)}");
            log.WriteLine($"  moved on pass 1:       {Count(movedFirst)}");
            log.WriteLine($"  moved again on pass 2: {Count(movedSecond)}");

            if (KeyFlipSweep.IsBrokenMeasurement(files.Count, answered)
                || KeyFlipSweep.IsBrokenMeasurement(answered, movedFirst)) {
                log.WriteLine(
                    "  ⚠ NOT A FINDING, A BROKEN MEASUREMENT: the oracle answered nothing, or answered "
                    + "every file with the file it was given. There is no first pass to be a fixed point of."
                );

                return 4;
            }

            foreach (var file in unstable) {
                log.WriteLine("  ⚠ not a fixed point after one invocation: " + file);
            }

            // Skala's side, over the same subtree: the pipeline reports its own pass count, so the
            // symmetrical question is answered from the run rather than from the property test.
            var compilation = ArrangementDifferential.Compile(files);
            var passes = new Dictionary<int, int>();
            var notConverged = new List<string>();
            foreach (var file in files) {
                var result = ArrangementDifferential.Run(file, compilation);
                passes[result.Passes] = passes.GetValueOrDefault(result.Passes) + 1;
                if (!result.Converged) {
                    notConverged.Add(file.ToString());
                }
            }

            log.WriteLine(
                "  skala passes to a fixed point: "
                + string.Join(
                    ", ",
                    passes.OrderBy(static pair => pair.Key)
                        .Select(static pair => Count(pair.Key) + "×" + Count(pair.Value))
                )
            );

            foreach (var file in notConverged) {
                log.WriteLine("  ⚠ skala did not converge: " + file);
            }

            return movedSecond == 0 && notConverged.Count == 0 ? 0 : 1;
        } finally {
            try {
                scratch.Delete(true);
            } catch (IOException) {
                // A scratch directory the tool still holds open is not worth failing over.
            }
        }
    }

    /// <summary>The first line on which two outputs part company, in both spellings.</summary>
    static string Difference(string first, string second) {
        var left = TextNormalisation.Lines(TextNormalisation.Normalise(first));
        var right = TextNormalisation.Lines(TextNormalisation.Normalise(second));
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++) {
            var one = i < left.Length ? left[i] : "(end of file)";
            var two = i < right.Length ? right[i] : "(end of file)";
            if (!string.Equals(one, two, StringComparison.Ordinal)) {
                return $"{Environment.NewLine}      line {Count(i + 1)} pass 1 │ {one}"
                    + $"{Environment.NewLine}      line {Count(i + 1)} pass 2 │ {two}";
            }
        }

        return string.Empty;
    }

    static bool Same(string left, string right) =>
        string.Equals(
            TextNormalisation.Normalise(left),
            TextNormalisation.Normalise(right),
            StringComparison.Ordinal
        );

    static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
