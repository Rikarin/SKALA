using Rikarin.Skala.Analysis.Hosting;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     <c>supersedes</c>: one span, one finding (docs/plan/08 § "Rule metadata").
/// </summary>
/// <remarks>
///     ⚠ Where a hosted analyzer's rule and a Skala rule both fire on the same span, one is dropped and
///     which one is a documented, deterministic choice: the superseding rule wins, and the superseded
///     one stays in the report marked suppressed with the reason, so the SARIF still says the other
///     analyzer had an opinion.
/// </remarks>
public static class Supersession {
    /// <summary>
    ///     The compiler warnings that state the same fact as a hosted Roslyn id, on the same span.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured, not assumed: on a field that is never used, <c>CS0169</c> and <c>IDE0051</c>
    ///     both report at the field's name; on a field initialised with a constant and never read,
    ///     <c>CS0414</c> and <c>IDE0051</c> do; on <c>int constant = 5;</c>, <c>CS0219</c> and
    ///     <c>IDE0059</c> both report at the local's name. The compiler's warning is on at stock
    ///     settings, so it is the one kept. A catalogue rule cannot say this through <c>supersedes</c>,
    ///     because the winner is not a catalogue rule.
    /// </remarks>
    static readonly (string Superseded, string Winner)[] CompilerWins = [
        (RoslynCodeStyle.UnusedMemberId, "CS0169"),
        (RoslynCodeStyle.UnusedMemberId, "CS0414"),
        (RoslynCodeStyle.UnreadMemberId, "CS0169"),
        (RoslynCodeStyle.UnreadMemberId, "CS0414"),
        (RoslynCodeStyle.UnusedValueId, "CS0219")
    ];

    // ⚠ After `CompilerWins`, which `Build` reads: static initializers run in textual order.
    static readonly Dictionary<string, List<string>> SupersededBy = Build();

    static Dictionary<string, List<string>> Build() {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in RuleCatalog.All) {
            foreach (var superseded in rule.Supersedes) {
                Add(map, superseded, rule.Id);
            }
        }

        foreach (var (superseded, winner) in CompilerWins) {
            Add(map, superseded, winner);
        }

        return map;
    }

    static void Add(Dictionary<string, List<string>> map, string superseded, string winner) {
        if (!map.TryGetValue(superseded, out var winners)) {
            winners = [];
            map[superseded] = winners;
        }

        winners.Add(winner);
    }

    public static ImmutableArray<Finding> Apply(ImmutableArray<Finding> findings) {
        if (SupersededBy.Count == 0) {
            return findings;
        }

        var winnerIds = SupersededBy.Values.SelectMany(static ids => ids).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var winners = findings.Where(finding => winnerIds.Contains(finding.RuleId))
            .Select(static finding => (finding.RuleId.ToUpperInvariant(), finding.Path, finding.Line, finding.Column))
            .ToHashSet();

        var builder = ImmutableArray.CreateBuilder<Finding>(findings.Length);
        foreach (var finding in findings) {
            builder.Add(
                IsSuperseded(finding, winners) ? finding with { Suppression = SuppressionKind.Superseded } : finding
            );
        }

        return builder.ToImmutable();
    }

    static bool IsSuperseded(Finding finding, HashSet<(string, string, int, int)> winners) {
        if (!SupersededBy.TryGetValue(finding.RuleId, out var candidates)) {
            return false;
        }

        foreach (var winner in candidates) {
            if (winners.Contains((winner.ToUpperInvariant(), finding.Path, finding.Line, finding.Column))) {
                return true;
            }
        }

        return false;
    }
}
