using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2200</c>–<c>SK2202</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule needs one more condition", and every "should not fire" fixture passes,
///     which reads as a spotless false-positive record. The shared harness filters to the fixture's own
///     rule id and never sees <c>AD0001</c> (issue #279); <c>skala check</c> records it only in the
///     SARIF's <c>toolExecutionNotifications</c> and does not fail the gate (issue #295). So these
///     tests do.
///     <para>
///         This batch has a specific reason to worry. <c>SK2200</c> walks a whole type through
///         <c>RegisterSymbolAction</c> and dereferences constructor and field syntax that is null or of
///         an unexpected node kind in ordinary code — a primary constructor, a record's copy
///         constructor, a partial declaration — and <c>SK2202</c> walks <em>up</em> from a modification
///         through parents that run out at a compilation unit.
///     </para>
/// </remarks>
public sealed class EventAndEffectBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new OverwrittenFieldInitializerAnalyzer(), new AnonymousUnsubscriptionAnalyzer(),
        new ConditionalInvocationSideEffectAnalyzer()
    ];

    static readonly string[] Batch = ["SK2200", "SK2201", "SK2202"];

    /// <summary>Every fixture in the batch, asserting only that no analyzer threw.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void NoFixture_CrashesAnAnalyzer(string path) {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(File.ReadAllText(path), path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    public static TheoryData<string> Fixtures {
        get {
            var data = new TheoryData<string>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Batch.Contains(fixture.RuleId, StringComparer.Ordinal)) {
                    data.Add(fixture.Path);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    /// <remarks>
    ///     Without this, three analyzers that all returned at their first guard would report a spotless
    ///     "no <c>AD0001</c>" over thirty files, which is the exact shape of the failure the crash test
    ///     exists to catch. What is asserted is the weakest fact that cannot hold vacuously: each of the
    ///     three really does produce at least one finding across the fixture set.
    /// </remarks>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire || !Batch.Contains(fixture.RuleId, StringComparer.Ordinal)) {
                continue;
            }

            foreach (var diagnostic in RuleFixtures.Analyze(
                         RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                         Analyzers,
                         TestContext.Current.CancellationToken
                     )) {
                seen.Add(diagnostic.Id);
            }
        }

        Assert.Equal(Batch, seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     The declarations a whole-type walk meets and a node walk does not, in one compilation.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Each of these is source the rules
    ///     have to survive rather than source they have to judge, and pinning the verdicts here would
    ///     turn a robustness test into a second copy of the fixtures that has to be edited whenever a
    ///     boundary moves — the version of this test that quietly stops testing anything.
    /// </remarks>
    [Fact]
    public void DegenerateDeclarations_DoNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Collections.Generic;

                              interface IHasDefault {
                                  int Width => 4;
                              }

                              record struct Pair(int Left, int Right);

                              record Positional(string Name) {
                                  readonly string upper = "";

                                  public string Upper => upper + Name;
                              }

                              struct WithInitializer {
                                  int slot = 1;

                                  public WithInitializer() => slot = 2;

                                  public int Slot => slot;
                              }

                              class Primary(int given) {
                                  readonly int width = 800;

                                  public int Width => width + given;
                              }

                              static class NoInstance {
                                  public static readonly int Seed = 3;
                              }

                              partial class Split {
                                  readonly int size = 1;

                                  public int Size => size;
                              }

                              partial class Split {
                                  public Split(int given) => size = given;
                              }

                              class Generic<T> where T : class {
                                  readonly T? cached = null;

                                  public Generic(T given) => cached = given;

                                  public T? Cached => cached;
                              }

                              class Evented {
                                  EventHandler? handlers;

                                  public event EventHandler? Changed {
                                      add => handlers += value;
                                      remove => handlers -= value;
                                  }

                                  public void Detach() => handlers -= (s, e) => { };

                                  public void Raise() => handlers?.Invoke(this, EventArgs.Empty);
                              }

                              class Accessed {
                                  int index;

                                  readonly Dictionary<string, int> map = new();

                                  public int Read(Dictionary<string, int>? other) => other?[Key(index++)] ?? map.Count;

                                  static string Key(int value) => value.ToString();
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Degenerate.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the degenerate compilation: it must really reach the rules.
    /// </summary>
    /// <remarks>
    ///     "Nothing threw" and "nothing ran" are the same green. This asserts that two of the three
    ///     still speak on that source, so the silence above is about robustness rather than about a
    ///     guard that returned first.
    /// </remarks>
    [Fact]
    public void TheDegenerateDeclarations_ReallyReachTheRules() {
        const string source = """
                              using System;

                              class Evented {
                                  EventHandler? handlers;

                                  int index;

                                  public void Detach() => handlers -= (s, e) => { };

                                  public void Bump(Action<int>? sink) => sink?.Invoke(index++);
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Degenerate.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "SK2201");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "SK2202");
    }

    /// <summary>
    ///     ⚠ The batch's boundary against <c>SK2064</c>, asserted rather than argued.
    /// </summary>
    /// <remarks>
    ///     <c>SK2064</c> reports <c>&amp;</c> written where <c>&amp;&amp;</c> was meant and declines any
    ///     right operand with a side effect, because a side effect there is the documented reason to
    ///     reach for the non-short-circuiting operator. <c>SK2202</c> takes the opposite side of the
    ///     same fact — a side effect that is skipped is the finding — so if the two ever met, one of
    ///     them would be wrong. They cannot: neither <c>&amp;</c> nor <c>&amp;&amp;</c> appears anywhere
    ///     in <c>SK2202</c>'s shape, and <c>?.</c> appears nowhere in <c>SK2064</c>'s. This is that on
    ///     the one file where it would show.
    /// </remarks>
    [Fact]
    public void AShortCircuitCarryingASideEffect_IsNotThisBatchesConcern() {
        const string source = """
                              using System;

                              sealed class Gate {
                                  int attempts;

                                  public bool Both(bool left) => left & Bump();

                                  public bool Short(bool left) => left && Bump();

                                  public string Coalesced(string? text) => text ?? Build();

                                  bool Bump() {
                                      attempts++;
                                      return true;
                                  }

                                  string Build() {
                                      attempts++;
                                      return "built";
                                  }
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "Gate.cs"), Analyzers, TestContext.Current.CancellationToken)
            .ToArray();

        Assert.Empty(produced);
    }

    /// <summary>
    ///     ⚠ <c>SK2200</c> against <c>SK2134</c>, which is the neighbour it could be confused with.
    /// </summary>
    /// <remarks>
    ///     Both are about a write that overwrites something. <c>SK2134</c> reports an instance member
    ///     assigning its own type's <em>static</em> field; <c>SK2200</c> reports an <em>instance</em>
    ///     field initializer that every constructor replaces. A type carrying both shapes must produce
    ///     one <c>SK2200</c> on the instance field and nothing at all on the static one — this rule
    ///     never visits a static field, so the two cannot double-count a line.
    /// </remarks>
    [Fact]
    public void AStaticFieldIsNeverThisRulesConcern() {
        const string source = """
                              sealed class Counter {
                                  static int total = 0;

                                  readonly int step = 1;

                                  public Counter(int given) {
                                      step = given;
                                      total = given;
                                  }

                                  public int Step => step;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "Counter.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id == "SK2200")
            .ToArray();

        Assert.Single(produced);
        Assert.Contains("step", produced[0].GetMessage(), StringComparison.Ordinal);
    }
}
