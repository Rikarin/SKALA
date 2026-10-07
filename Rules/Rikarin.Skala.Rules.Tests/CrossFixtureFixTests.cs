using Microsoft.CodeAnalysis;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     Every safe fix of every rule, applied to every fixture in the repository, and recompiled.
/// </summary>
/// <remarks>
///     ⚠ <see cref="FixRoundTripTests" /> round-trips a rule's fix on the rule's <b>own</b> positive
///     fixtures and nowhere else, so a finding a rule makes on <em>another</em> rule's fixture is
///     reported, carries an edit marked safe, and is never applied by anything. Both of
///     [#402](https://github.com/Rikarin/SKALA/issues/402) and [#403](https://github.com/Rikarin/SKALA/issues/403)
///     lived there: <c>SK1053</c> turned <c>Local local = new();</c> into <c>_ = new();</c> (CS8754) on
///     an <c>SK7081</c> fixture, and <c>SK0241</c> deleted the <c>abstract</c> that re-abstracts a base
///     interface's default on an <c>SK0280</c> fixture (CS0501). Each rule's own fixtures held neither
///     shape, because each author wrote the shapes they had thought of.
///     <para>
///         The fixture corpus is the largest body of deliberately varied C# in the repository, and
///         nearly every file in it is somebody else's edge case. Applying one rule's edits at a time —
///         grouped per rule, as <c>skala fix</c> applies a pass — keeps a regression attributable to
///         the rule that caused it; two rules' edits that are each sound and conflict together are
///         <c>skala fix</c>'s re-analysis problem, not a fix defect.
///     </para>
///     <para>
///         ⚠ It is one <see cref="FactAttribute" /> over a parallel loop rather than a theory with a row
///         per fixture: xunit runs a theory's rows serially, and this is one compilation per
///         (fixture, rule) pair on top of an analysis per fixture.
///     </para>
/// </remarks>
public sealed class CrossFixtureFixTests {
    [Fact]
    public void EverySafeFix_OnEveryFixture_IntroducesNoCompilerError() {
        var cancellation = TestContext.Current.CancellationToken;
        var fixtures = RuleFixtures.All();
        var failures = new ConcurrentBag<string>();
        var applied = 0;

        Parallel.ForEach(
            fixtures,
            new ParallelOptions { CancellationToken = cancellation },
            fixture => {
                foreach (var failure in Regressions(File.ReadAllText(fixture.Path), fixture.Path, ref applied, cancellation)) {
                    failures.Add(CrossRuleBaseline.Key(fixture.Path) + ": " + failure);
                }
            }
        );

        // Anti-vacuity: a sweep that applied nothing passes for the reason a disabled check passes.
        Assert.True(applied > 1000, $"Only {applied} (fixture, rule) fix(es) were applied across {fixtures.Count} fixtures.");
        Assert.True(
            failures.IsEmpty,
            $"{failures.Count} safe fix(es) out of {applied} applied break a fixture that compiled before:\n  "
            + string.Join("\n  ", failures.Order(StringComparer.Ordinal))
        );
    }

    /// <summary>
    ///     ⚠ The instrument check: an edit that breaks the build must come back as a regression, and the
    ///     one that is sound must not.
    /// </summary>
    [Fact]
    public void TheComparison_SeesABreakingEdit_AndNotASoundOne() {
        const string source = "class C { void M() { var x = 1; } }";
        var cancellation = TestContext.Current.CancellationToken;
        var before = RuleFixtures.Compile(source, "planted.cs");

        Assert.Equal(["CS8754 ×1"], NewErrors(before, source.Replace("var x = 1", "_ = new()", StringComparison.Ordinal), "planted.cs", cancellation));
        Assert.Empty(NewErrors(before, source.Replace("var x = 1", "_ = 1", StringComparison.Ordinal), "planted.cs", cancellation));
    }

    /// <summary>Each safe-fix rule's edits on one source, applied rule by rule, and what they broke.</summary>
    static List<string> Regressions(string source, string path, ref int applied, CancellationToken cancellation) {
        var before = RuleFixtures.Compile(source, path);
        var findings = RuleFixtures.Analyze(before, SkalaAnalyzers.All, cancellation);
        var result = new List<string>();

        foreach (var group in findings
                     .Where(static diagnostic => RuleCatalog.Find(diagnostic.Id) is { HasFix: true, FixIsSafe: true })
                     .GroupBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal)) {
            var edits = group.SelectMany(FixRoundTripTests.ReadEdits).OrderByDescending(static edit => edit.Start).ToList();
            if (edits.Count == 0) {
                continue;
            }

            var text = source;
            var consumed = int.MaxValue;
            foreach (var (start, length, replacement) in edits) {
                // As in FixRoundTripTests: an overlapping edit is the next pass's business.
                if (start + length > consumed) {
                    continue;
                }

                text = text[..start] + replacement + text[(start + length)..];
                consumed = start;
            }

            Interlocked.Increment(ref applied);
            var broken = NewErrors(before, text, path, cancellation);
            if (broken.Length > 0) {
                result.Add(group.Key + " introduces " + string.Join(", ", broken));
            }
        }

        return result;
    }

    /// <summary>The error ids the edited text has more of than the original, counted per id.</summary>
    /// <remarks>
    ///     ⚠ Per id rather than per (line, id), as in <see cref="FixRoundTripTests" />: an edit that
    ///     removes a line moves every error below it, and a line-keyed comparison calls the move a
    ///     regression.
    /// </remarks>
    static ImmutableArray<string> NewErrors(Compilation before, string text, string path, CancellationToken cancellation) {
        var was = FixRoundTripTests.ErrorsById(before, cancellation);
        var now = FixRoundTripTests.ErrorsById(RuleFixtures.Compile(text, path), cancellation);
        return [
            .. now
                .Where(entry => entry.Value > was.GetValueOrDefault(entry.Key))
                .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key
                    + " ×"
                    + (entry.Value - was.GetValueOrDefault(entry.Key)).ToString(CultureInfo.InvariantCulture)
                )
        ];
    }
}
