using System.Text.RegularExpressions;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Testing;

/// <summary>One minimised fuzz finding that has not been fixed yet.</summary>
/// <param name="Probe">
///     The name of the <see cref="OpenDefectProbe" /> that removes this defect's trigger, or the empty
///     string when its cause is not established.
/// </param>
/// <remarks>
///     ⚠ An entry with no <c>probe:</c> is an entry the expedition <b>cannot</b> tell apart from a new
///     defect, so the nightly still goes red when the fuzzer rediscovers it. That is the mechanism
///     refusing to guess rather than a gap in it: naming a trigger is a claim about the cause, and an
///     entry whose own status line says "cause not established" has no claim to make. The way to stop
///     such an entry reding the nightly is to diagnose it — which is the pressure this field is for.
/// </remarks>
public sealed record OpenDefect(string Id, string Summary, string File, string Property, string Seed, string Probe = "") {
    public string Path => System.IO.Path.Combine(OpenDefects.Root, File);

    /// <summary>
    ///     The unmutated half, for an absorption entry. <c>null</c> when there is none.
    /// </summary>
    /// <remarks>
    ///     ⚠ Absorption is a statement about a <b>pair</b> — <c>format(mutate(x)) ≡ format(x)</c> — and
    ///     there is no single string that carries it. One file would let the entry rot into "this file
    ///     formats to something", which is true of every file.
    /// </remarks>
    public string? BaselinePath {
        get {
            var candidate = System.IO.Path.ChangeExtension(Path, null) + ".baseline.cs";
            return System.IO.File.Exists(candidate) ? candidate : null;
        }
    }

    public override string ToString() => Id + " (" + File + ")";
}

/// <summary>
///     The register in <c>Testing/corpus/pathological/open/register.md</c>.
/// </summary>
/// <remarks>
///     ⚠ Read out of a markdown file rather than declared in code, for the reason
///     <see cref="Divergences" /> is: the argument for each entry is the point, an argument does not fit
///     in an attribute, and the person who has to decide whether a defect is still worth having is
///     reading prose. The code here needs four fields; the file carries the case.
/// </remarks>
public static class OpenDefects {
    public static string Root { get; } =
        Path.Combine(Corpus.SetRoot(Corpus.Pathological), OpenDirectory);

    /// <summary>
    ///     ⚠ Excluded from <see cref="Corpus.Files" />. See the register for why.
    /// </summary>
    public const string OpenDirectory = "open";

    public static string RegisterPath { get; } = Path.Combine(Root, "register.md");

    public static IReadOnlyList<OpenDefect> Register { get; } = Read();

    /// <summary>
    ///     The <c>.cs</c> files in the directory, which the register must account for exactly.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>*.baseline.cs</c> is excluded on the same argument that excludes <c>*.expected.cs</c>
    ///     from the corpus: it is the other half of an entry, not an entry.
    /// </remarks>
    public static IReadOnlyList<string> Files() =>
        Directory.Exists(Root)
            ? [
                .. Directory.EnumerateFiles(Root, "*.cs", SearchOption.TopDirectoryOnly)
                    .Where(static path => !path.EndsWith(".baseline.cs", StringComparison.Ordinal))
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)
            ]
            : [];

    /// <summary>
    ///     The register entry that accounts for a violation, or <c>null</c> when it is a new defect.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This is the check the nightly's failure condition rests on, so here is exactly what it
    ///     does and exactly what it guarantees.</b>
    ///     <para>
    ///         For each entry that names the same property <i>and</i> names a probe, it takes the
    ///         probe's neutralisation of this input — the registered trigger removed and nothing else —
    ///         and asks <see cref="FuzzProperties.Check" /> again. The entry accounts for the violation
    ///         only if the property now <b>holds</b>. Three things are refused before that: a
    ///         neutralisation that found no trigger, one that changed nothing, and one that made the
    ///         input parse worse than it did (ADR-003 leaves an input that lost its parse byte-identical,
    ///         so every property holds over it for free and a broken probe would otherwise look like a
    ///         successful one).
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The safety argument, in one sentence:</b> a finding is accounted for only when
    ///         deleting the registered trigger makes the property hold, so an input that carries a
    ///         second, unregistered defect still fails after the deletion and is reported as new. A new
    ///         defect can therefore only hide behind a registered one if the registered trigger is what
    ///         causes it — in which case it is the registered defect. The property name selects which
    ///         entries are even worth trying; it never decides anything. That is the difference between
    ///         this and the rule-id match the register's own SK-FUZZ-0016 entry argues against.
    ///     </para>
    ///     <para>
    ///         ⚠ It is called on <b>every</b> violation, before the per-property deduplication in
    ///         <see cref="Fuzzer.Run" /> and never after it. That ordering is load-bearing: the run
    ///         reports the first violation of each property and counts the rest, so screening after the
    ///         dedup would let a registered defect take the property's one slot and swallow every new
    ///         defect of the same property behind it. Screening first costs one extra property check per
    ///         violation — violations are rare against the case count — and closes that hole completely.
    ///     </para>
    ///     <para>
    ///         ⚠ What it does <b>not</b> guarantee: that a probe is as narrow as it claims. A probe that
    ///         deleted most of the file would make every property hold and would account for anything.
    ///         Nothing here can detect that, which is why <see cref="OpenDefectProbes.All" /> is a closed
    ///         vocabulary reviewed beside the entries it serves, and why <c>OpenDefectTests</c> requires
    ///         each probe to fire on its own entry's fixture and to be the reason that fixture fails.
    ///     </para>
    /// </remarks>
    public static OpenDefect? Explain(
        string property,
        string path,
        string source,
        in FormattingOptions options,
        IReadOnlyList<string> symbols,
        bool arrangement,
        CancellationToken cancellation = default
    ) {
        foreach (var entry in Register) {
            if (!string.Equals(entry.Property, property, StringComparison.Ordinal)) {
                continue;
            }

            if (entry.Probe is not { Length: > 0 } name) {
                continue;
            }

            // ⚠ An unknown probe name throws rather than being skipped. Skipping would turn a typo in
            // the register into a silently red nightly whose cause is a markdown field, and the entry
            // would look like it had a probe.
            var probe = OpenDefectProbes.Find(name)
                ?? throw new InvalidOperationException(
                    $"{entry.Id} names probe `{name}`, which is not in OpenDefectProbes.All. "
                    + "The vocabulary is closed; add it there, with the argument, or fix the register."
                );

            if (probe.Neutralise(source) is not { } without
                || string.Equals(without, source, StringComparison.Ordinal)
                || !OpenDefectProbes.ParsesNoWorse(source, without)) {
                continue;
            }

            var after = FuzzProperties.Check(
                path,
                without,
                options,
                symbols,
                null,
                arrangement
                || property is FuzzProperties.ArrangementIdempotency or FuzzProperties.ArrangementConvergence,
                cancellation: cancellation
            );

            // ⚠ Still failing without the registered trigger: there is something else in this input,
            // and the entry does not account for it. Reported as new, which is the whole point.
            if (after.Any(violation => string.Equals(violation.Property, property, StringComparison.Ordinal))) {
                continue;
            }

            return entry;
        }

        return null;
    }

    static List<OpenDefect> Read() {
        var entries = new List<OpenDefect>();
        if (!File.Exists(RegisterPath)) {
            return entries;
        }

        string? id = null;
        string? summary = null;
        string? file = null;
        string? property = null;
        string? seed = null;
        string? probe = null;

        foreach (var line in File.ReadLines(RegisterPath)) {
            var heading = Regex.Match(line, @"^##\s+(SK-FUZZ-\d{4})\s*—\s*(.+)$");
            if (heading.Success) {
                Flush(entries, ref id, ref summary, ref file, ref property, ref seed, ref probe);
                id = heading.Groups[1].Value;
                summary = heading.Groups[2].Value.Trim();
                continue;
            }

            if (id is null) {
                continue;
            }

            var field = Regex.Match(line, @"^-\s+(file|property|seed|probe):\s*`?([^`]+)`?\s*$");
            if (!field.Success) {
                continue;
            }

            switch (field.Groups[1].Value) {
                case "file":
                    file = field.Groups[2].Value.Trim();
                    break;
                case "property":
                    property = field.Groups[2].Value.Trim();
                    break;
                case "probe":
                    probe = field.Groups[2].Value.Trim();
                    break;
                default:
                    seed = field.Groups[2].Value.Trim();
                    break;
            }
        }

        Flush(entries, ref id, ref summary, ref file, ref property, ref seed, ref probe);
        return entries;
    }

    static void Flush(
        List<OpenDefect> entries,
        ref string? id,
        ref string? summary,
        ref string? file,
        ref string? property,
        ref string? seed,
        ref string? probe
    ) {
        if (id is not null && file is not null && property is not null) {
            entries.Add(
                new OpenDefect(id, summary ?? string.Empty, file, property, seed ?? string.Empty, probe ?? string.Empty)
            );
        }

        id = null;
        summary = null;
        file = null;
        property = null;
        seed = null;
        probe = null;
    }
}
