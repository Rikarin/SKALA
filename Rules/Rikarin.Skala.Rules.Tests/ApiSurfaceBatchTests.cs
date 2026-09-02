using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Design;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The checks <see cref="RuleFixtureTests" /> does not make, for <c>SK6060</c>–<c>SK6062</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception,
///         turns it into <c>AD0001</c> and returns nothing from that analyzer — so every "should not
///         fire" fixture goes green, the positives go red, and the failure reads as a rule that does
///         not work rather than one that threw. The shared harness does not look (issue #279) and
///         <c>skala check</c> drops it into <c>toolExecutionNotifications</c> (issue #295). This file
///         looks.
///     </para>
///     <para>
///         ⚠ The second half is the one <see cref="RuleFixtureTests" /> structurally cannot make. A
///         fixture asserts "no finding in this file", so a guard whose <em>only</em> job is to
///         suppress one finding among several in the same file is invisible to it — and every
///         <c>SK6061</c> guard is exactly that shape. These name the count and the subject.
///     </para>
/// </remarks>
public sealed class ApiSurfaceBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Batch = [
        new InvariantTypeParameterAnalyzer(), new CallerInfoParameterOrderAnalyzer(),
        new WriteOnlyLocalCollectionAnalyzer()
    ];

    static readonly string[] Ids = [
        RuleIds.InvariantTypeParameter, RuleIds.CallerInfoParameterNotLast, RuleIds.WriteOnlyLocalCollection
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Ids.Contains(fixture.RuleId)) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>⚠ Anti-vacuity: an empty theory is the shape of this file having stopped working.</summary>
    [Fact]
    public void TheBatch_HasFixturesToCheck() {
        var all = RuleFixtures.All();
        foreach (var id in Ids) {
            Assert.True(
                all.Count(fixture => fixture.RuleId == id) >= 10,
                $"{id} has fewer than ten fixtures; the checks below would be nearly vacuous."
            );
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoAnalyzerThrows(RuleFixture fixture) {
        var source = File.ReadAllText(fixture.Path);
        var produced = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, fixture.Path),
            Batch,
            TestContext.Current.CancellationToken
        );

        var crashes = produced.Where(static diagnostic => diagnostic.Id == "AD0001").ToArray();
        Assert.True(
            crashes.Length == 0,
            $"{fixture}: an analyzer threw, which silently passes every negative fixture:\n  "
            + string.Join("\n  ", crashes.Select(static d => d.GetMessage()))
        );
    }

    /// <summary>
    ///     ⚠ The declaration that owns the parameter order is reported exactly once, and the
    ///     declarations that copy it are not reported at all.
    /// </summary>
    /// <remarks>
    ///     A negative fixture cannot assert this: the base declaration in each of these sources
    ///     carries the same defect and is the place the rule is right to report it, so a file
    ///     containing both would fire and prove nothing about the guard. Naming the count and the
    ///     subject is the only form of the assertion that can fail for the right reason.
    /// </remarks>
    [Theory]
    [InlineData(
        """
        using System.Runtime.CompilerServices;

        public abstract class Base {
            public abstract void Log(string message, [CallerMemberName] string caller = "", int level = 0);
        }

        public sealed class Derived : Base {
            public override void Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
        }
        """
    )]
    [InlineData(
        """
        using System.Runtime.CompilerServices;

        public interface ISink {
            void Log(string message, [CallerMemberName] string caller = "", int level = 0);
        }

        public sealed class Sink : ISink {
            public void Log(string message, [CallerMemberName] string caller = "", int level = 0) { }
        }
        """
    )]
    public void TheOrderIsReportedOnceOnTheDeclarationThatOwnsIt(string source) {
        var produced = Findings(source, RuleIds.CallerInfoParameterNotLast);

        Assert.True(
            produced.Length == 1,
            $"expected exactly one finding on the declaration that owns the order, got {produced.Length}:\n  "
            + string.Join("\n  ", produced.Select(static d => d.Location.GetLineSpan() + ": " + d.GetMessage()))
        );

        Assert.Equal(3, produced[0].Location.GetLineSpan().StartLinePosition.Line);
    }

    /// <summary>
    ///     ⚠ Sabotage, and it is the only check that distinguishes the composition from a coincidence.
    /// </summary>
    /// <remarks>
    ///     <c>SK6060</c>'s whole content is that a type parameter's position is composed through the
    ///     declared variance of every generic enclosing it. Delete that composition and the three
    ///     sources below become indistinguishable — all of them are "the parameter appears in a return
    ///     type" — while every fixture keyed on a bare <c>T</c> stays green. Asserting the three
    ///     different answers is what makes the composition falsifiable.
    /// </remarks>
    [Theory]
    [InlineData("System.Collections.Generic.IEnumerable<T> Get();", "out")]
    [InlineData("System.Action<T> Get();", "in")]
    [InlineData("System.Collections.Generic.List<T> Get();", null)]
    [InlineData("System.Func<T, T> Get();", null)]
    [InlineData("ref T Get();", null)]
    public void TheSamePositionGivesThreeAnswersDependingOnWhatEnclosesIt(string member, string? expected) {
        var produced = Findings(
            "public interface IProbe<T> {\n    " + member + "\n}\n",
            RuleIds.InvariantTypeParameter
        );

        if (expected is null) {
            Assert.True(
                produced.Length == 0,
                $"`{member}` puts the parameter where no modifier is valid, and SK6060 offered one: "
                + string.Join("; ", produced.Select(static d => d.GetMessage()))
            );

            return;
        }

        Assert.Single(produced);
        Assert.Contains("`" + expected + " T`", produced[0].GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ Sabotage for <c>SK6062</c>: the discarded return value is a requirement, not a detail.
    /// </summary>
    /// <remarks>
    ///     The two sources differ by one <c>if</c>. Drop the "the invocation's parent is an expression
    ///     statement" condition and both become "a mutating call on the local", the rule reports both,
    ///     and every fixture in the set still passes — because none of the others reads a collection
    ///     through a returned value.
    /// </remarks>
    [Theory]
    [InlineData("        seen.Remove(item);\n", true)]
    [InlineData("        if (seen.Remove(item)) { }\n", false)]
    public void ReadingTheCollectionThroughAReturnValueIsAUse(string statement, bool expected) {
        var produced = Findings(
            """
            using System.Collections.Generic;

            public static class Probe {
                public static void Run(IEnumerable<string> items) {
                    var seen = new HashSet<string>();
                    foreach (var item in items) {
                        seen.Add(item);
            """
            + statement
            + """
                    }
                }
            }
            """,
            RuleIds.WriteOnlyLocalCollection
        );

        Assert.Equal(expected, produced.Length == 1);
    }

    static Diagnostic[] Findings(string source, string ruleId) {
        var compilation = RuleFixtures.Compile(source, "probe.cs");
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        // ⚠ An inline source that does not compile answers "no finding" for the wrong reason, and the
        // negative half of every assertion here would pass on it.
        Assert.True(
            errors.Length == 0,
            "the inline probe does not compile, so it proves nothing: "
            + string.Join("; ", errors.Take(3).Select(static d => d.ToString()))
        );

        var produced = RuleFixtures.Analyze(compilation, Batch, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(produced, static diagnostic => diagnostic.Id == "AD0001");

        return produced.Where(diagnostic => diagnostic.Id == ruleId).ToArray();
    }
}
