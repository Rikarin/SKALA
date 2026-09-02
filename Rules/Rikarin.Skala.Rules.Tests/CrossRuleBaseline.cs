using Microsoft.CodeAnalysis;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Which other rules are known to fire on which negative fixture, and the measurement that says so.
/// </summary>
/// <remarks>
///     ⚠ The fixture harness runs each rule against its own directory and throws away everything else
///     it saw — so a fixture written for one rule can contain the defect a different rule reports, and
///     nothing anywhere fails. Sweeping the four new <c>SK21xx</c> attribute analyzers over every
///     fixture in the repository found two such files on the first attempt, both true positives
///     ([#285](https://github.com/Rikarin/SKALA/issues/285)). ⚠
///     <b>
///         The point was never the two files.
///         It is that a cross-fixture sweep is a measurement nobody was taking
///     </b>, over the corpus Skala
///     uses to prove its rules correct.
///     <para>
///         The sweep now runs on every fixture, inside the assertion that was already computing every
///         diagnostic — it costs no extra compilation — and is compared for <em>equality</em> against
///         <c>fixture-cross-rule-baseline.txt</c>. Equality rather than containment is what makes it an
///         instrument instead of an allow-list: a repaired fixture must delete its line, so the file
///         cannot fill up with entries that stopped being true.
///     </para>
/// </remarks>
public static class CrossRuleBaseline {
    /// <summary>The categories a cross-rule finding is measured in, and nothing else.</summary>
    /// <remarks>
    ///     ⚠ Measured, not chosen by taste: the same sweep across every category produces 18 030
    ///     findings on 3 848 fixtures, 15 843 of them <c>SK7101</c>, <c>SK7010</c> and <c>SK6030</c> —
    ///     documentation and declaration style, firing because a fixture is a deliberately minimal
    ///     snippet. A baseline of that size records nothing anybody would read. In these two
    ///     categories, "this file contains the defect another rule reports" is a statement about the
    ///     fixture rather than about its brevity.
    /// </remarks>
    public static ImmutableHashSet<string> Categories { get; } = ["Correctness", "Security"];

    public static string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(RuleFixtures.Root)!,
        "fixture-cross-rule-baseline.txt"
    );

    static ImmutableDictionary<string, ImmutableHashSet<string>> Entries { get; } = Read();

    /// <summary>The rules recorded as firing on one fixture, keyed by its path relative to fixtures/.</summary>
    public static ImmutableHashSet<string> For(string relativePath) =>
        Entries.TryGetValue(relativePath, out var rules) ? rules : [];

    /// <summary>Every (fixture, rule) pair the file records, for the rot check.</summary>
    public static IEnumerable<(string Fixture, string Rule)> All() =>
        Entries.SelectMany(static entry => entry.Value.Select(rule => (entry.Key, rule)));

    /// <summary>
    ///     The rules other than its own that a fixture's diagnostics carry, in the measured categories.
    /// </summary>
    /// <remarks>
    ///     ⚠ A diagnostic the catalogue does not know is skipped rather than counted: <c>AD0001</c> and
    ///     the compiler's own ids reach this list too, and both are somebody else's assertion.
    /// </remarks>
    public static ImmutableHashSet<string> Observed(ImmutableArray<Diagnostic> diagnostics, string ownRuleId) {
        var result = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var diagnostic in diagnostics) {
            if (diagnostic.Id != ownRuleId
                && RuleCatalog.Find(diagnostic.Id) is { } rule
                && Categories.Contains(rule.Category)) {
                result.Add(diagnostic.Id);
            }
        }

        return result.ToImmutable();
    }

    /// <summary>The fixture's path in the form the baseline records, so the two can be compared at all.</summary>
    public static string Key(string fixturePath) =>
        System.IO.Path.GetRelativePath(RuleFixtures.Root, fixturePath).Replace('\\', '/');

    static ImmutableDictionary<string, ImmutableHashSet<string>> Read() {
        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.Ordinal);
        if (!File.Exists(Path)) {
            return builder.ToImmutable();
        }

        foreach (var line in File.ReadAllLines(Path)) {
            if (line.Length == 0 || line[0] == '#') {
                continue;
            }

            var fields = line.Split('\t');
            if (fields.Length < 2) {
                throw new InvalidOperationException(
                    "fixture-cross-rule-baseline.txt: '" + line + "' is not <path> TAB <rule id> TAB <reason>."
                );
            }

            var fixture = fields[0].Trim();
            builder[fixture] = For(builder, fixture).Add(fields[1].Trim());
        }

        return builder.ToImmutable();
    }

    static ImmutableHashSet<string> For(
        ImmutableDictionary<string, ImmutableHashSet<string>>.Builder builder,
        string fixture
    ) =>
        builder.TryGetValue(fixture, out var rules)
            ? rules
            : ImmutableHashSet.Create<string>(StringComparer.Ordinal);
}
