using Microsoft.CodeAnalysis.CSharp;
using Rikarin.Skala.Analysis.Loading;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Analysis;

/// <summary>
///     Withholding a finding whose rewrite needs a language version one of the project's other target
///     frameworks does not have (#351).
/// </summary>
/// <remarks>
///     ⚠ <b>The second axis of #343's bug, and the larger one.</b> #343 closed the case where a rule
///     asks whether a <em>type</em> exists — <c>System.Threading.Lock</c> — and built
///     <see cref="FrameworkAvailability" /> for it. The same union defeats the
///     <see cref="SkalaRule.MeetsLanguageVersion" /> gate that roughly forty <c>SK1xxx</c> rules open
///     with, and nothing was guarding it.
///     <para>
///         ⚠ <b>Measured, because it is the whole premise and it is easy to get wrong.</b> A
///         <c>netstandard2.0;net10.0</c> project with <em>no</em> <c>&lt;LangVersion&gt;</c> of its own
///         evaluates <c>LangVersion</c> to <b>7.3</b> for the <c>netstandard2.0</c> moniker and
///         <b>14.0</b> for <c>net10.0</c> — one project, one source file, two language versions
///         (<c>dotnet msbuild -getProperty:LangVersion -p:TargetFramework=…</c>). So <c>SK1005</c>
///         fires from the <c>net10.0</c> leg, <c>skala fix --safe</c> rewrites the block namespace to a
///         file-scoped one, and the <c>netstandard2.0</c> leg stops compiling — #343's failure exactly,
///         with the language version in place of the missing type.
///     </para>
///     <para>
///         ⚠ <b>Why this is central rather than forty copies of #343's per-rule guard.</b> The floor is
///         already declarative: every such rule states it as <c>languageVersion</c> in
///         <c>rules.json</c> and reads it back as <see cref="RuleInfo.LanguageVersion" />. Nothing
///         rule-specific is left to ask, so there is nothing for a per-rule predicate to carry —
///         unlike <c>SK1023</c>, whose condition includes shape checks that tell the real
///         <c>System.Threading.Lock</c> from a source-declared one and therefore has to stay in the
///         analyzer. The two mechanisms are complementary: <see cref="FrameworkAvailability" /> for a
///         predicate only the rule can state, this for the floor every rule already declares.
///         <b>A rule added tomorrow with a <c>languageVersion</c> is guarded the day it lands</b>,
///         which a convention policed by a test is not.
///     </para>
///     <para>
///         ⚠ Applied after the per-unit loop and therefore after <c>IncrementalAnalysis</c>'s cache,
///         which is deliberate: the sibling set is not part of the cache fingerprint, so a guard
///         applied <em>inside</em> an analyzer can be baked into a cached result. Filtering the merged
///         findings cannot go stale that way.
///     </para>
/// </remarks>
public static class MultiTargetLanguageFloor {
    /// <summary>
    ///     <paramref name="findings" /> minus the ones whose rule declares a language floor that some
    ///     other moniker compiling the same file does not reach.
    /// </summary>
    public static ImmutableArray<Finding> Filter(
        IReadOnlyList<Finding> findings,
        ImmutableArray<CompilationUnit> units
    ) {
        if (findings.Count == 0 || LowestVersionPerPath(units) is not { } floors) {
            return [.. findings];
        }

        var kept = ImmutableArray.CreateBuilder<Finding>(findings.Count);
        foreach (var finding in findings.Where(finding => IsExpressibleEverywhere(finding, floors))) {
            kept.Add(finding);
        }

        return kept.ToImmutable();
    }

    static bool IsExpressibleEverywhere(Finding finding, Dictionary<string, LanguageVersion> floors) {
        // A rule with no declared floor makes no claim about the language version, so nothing here
        // can decide against it. This is the overwhelming majority of the catalogue.
        if (RuleCatalog.Find(finding.RuleId)?.LanguageVersion is not { } declared
            || !SkalaRule.TryParseLanguageVersion(declared, out var required)
            || !floors.TryGetValue(finding.Path, out var lowest)) {
            return true;
        }

        return lowest >= required;
    }

    /// <summary>
    ///     For each source file of a multi-targeted project, the lowest language version any of its
    ///     monikers compiles it at. Null when nothing in the load multi-targets.
    /// </summary>
    /// <remarks>
    ///     ⚠ Keyed on <c>Siblings</c> rather than on <c>units.Length &gt; 1</c>. A solution of two
    ///     single-target projects is also several units and multi-targets nothing;
    ///     <c>MultiTargetLink.Apply</c> is the one thing that has decided which units are monikers of
    ///     one project, and this reuses that decision rather than making a second, different one.
    ///     <para>
    ///         ⚠ Keyed on the path rather than on the project, for the reason
    ///         <see cref="FrameworkAvailability.PathsWithout" /> gives: a moniker may compile a
    ///         different set of files, so "this project has an older framework" is not the same claim
    ///         as "this file is compiled by one". A file linked into two projects takes the lowest of
    ///         both, which is the same conservative answer for the same reason.
    ///     </para>
    /// </remarks>
    static Dictionary<string, LanguageVersion>? LowestVersionPerPath(ImmutableArray<CompilationUnit> units) {
        Dictionary<string, LanguageVersion>? floors = null;
        foreach (var unit in units) {
            if (unit.Siblings.IsEmpty) {
                continue;
            }

            var version = unit.Compilation.LanguageVersion;
            foreach (var tree in unit.Compilation.SyntaxTrees) {
                if (tree.FilePath is not { Length: > 0 } path) {
                    continue;
                }

                floors ??= new Dictionary<string, LanguageVersion>(StringComparer.Ordinal);
                floors[path] = floors.TryGetValue(path, out var seen) && seen < version ? seen : version;
            }
        }

        return floors;
    }
}
