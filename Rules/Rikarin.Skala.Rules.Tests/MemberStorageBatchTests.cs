using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2130</c>–<c>SK2134</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail,
///     which reads as "the rule is wrong and needs another condition", and every "should not fire"
///     fixture passes, which reads as a spotless false-positive record. The fixture harness does not
///     look for <c>AD0001</c> (issue #279) and <c>skala check</c> drops it (issue #295), so these tests
///     do.
///     <para>
///         This batch has two specific reasons to worry about it. Two of the five rules walk a type's
///         <em>whole</em> declaration through <c>RegisterSymbolStartAction</c> rather than one node, so
///         they meet shapes a syntax-node rule never sees; and every one of the five dereferences a
///         symbol that is null in ordinary broken or exotic source.
///     </para>
/// </remarks>
public sealed class MemberStorageBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ForwardStaticInitializerAnalyzer(), new UnassignedGetOnlyPropertyAnalyzer(),
        new MismatchedBackingFieldAnalyzer(), new UnimplementedPartialMethodAnalyzer(),
        new InstanceWriteToStaticAnalyzer()
    ];

    /// <summary>
    ///     Every fixture in the batch, asserting only that no analyzer threw.
    /// </summary>
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
                if (fixture.RuleId is "SK2130" or "SK2131" or "SK2132" or "SK2133" or "SK2134") {
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
    ///     Without this, a batch whose five analyzers all returned at their first guard would report a
    ///     spotless "no <c>AD0001</c>" over fifty files, which is the exact shape of the failure the
    ///     crash test exists to catch. What is asserted is the weakest fact that cannot hold vacuously:
    ///     each of the five really does produce at least one finding across the fixture set.
    /// </remarks>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire
                || fixture.RuleId is not ("SK2130" or "SK2131" or "SK2132" or "SK2133" or "SK2134")) {
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

        Assert.Equal(["SK2130", "SK2131", "SK2132", "SK2133", "SK2134"], seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     The shapes a symbol walk meets and a node walk does not, in one compilation.
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

                              interface IHasStorage {
                                  int Width { get; }

                                  int Doubled => Width * 2;
                              }

                              record struct Pair(int Left, int Right);

                              record Positional(string Name) {
                                  public string Upper => Name.ToUpperInvariant();
                              }

                              static class Recursive {
                                  public static readonly int A = B;
                                  public static readonly int B = A;
                              }

                              unsafe struct Fixed {
                                  public fixed int Buffer[4];
                              }

                              partial class Outer {
                                  partial void Hook();

                                  class Nested {
                                      static int shared;

                                      public int Value { get; }

                                      public void Bump() => shared++;
                                  }

                                  public void Run() => Hook();
                              }

                              class Generic<T> where T : notnull {
                                  static T? cached;

                                  T? item;

                                  public T? Item {
                                      get => item;
                                      set => item = value;
                                  }

                                  public T? Cached { get; }

                                  public void Store(T value) => cached = value;
                              }

                              class Indexed {
                                  readonly int[] slots = new int[4];

                                  public int this[int index] {
                                      get => slots[index];
                                      set => slots[index] = value;
                                  }
                              }

                              class Evented {
                                  static EventHandler? handlers;

                                  public event EventHandler? Changed {
                                      add => handlers += value;
                                      remove => handlers -= value;
                                  }
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
    ///     The source above contains an <c>unsafe</c> member, which the fixture compilation does not
    ///     allow — so it also carries compile errors, and a rule reading an error type answers "no
    ///     finding" for the wrong reason. Asserting that something is still reported is what separates
    ///     "nothing threw" from "nothing ran".
    /// </remarks>
    [Fact]
    public void TheDegenerateDeclarations_ReallyReachTheRules() {
        const string source = """
                              partial class Outer {
                                  partial void Hook();

                                  class Nested {
                                      static int shared;

                                      public void Bump() => shared++;
                                  }

                                  public void Run() => Hook();
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Degenerate.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "SK2133");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "SK2134");
    }

    /// <summary>
    ///     ⚠ The batch's internal boundary, asserted rather than described.
    /// </summary>
    /// <remarks>
    ///     <c>SK2131</c> and <c>SK2132</c> are the pair that could collide: both read a property, and a
    ///     get-only property with a crossed getter is a shape both might claim. They cannot, and the
    ///     reason is structural rather than a filter — <c>SK2131</c> requires an <em>auto</em>-property,
    ///     which has no accessor body at all, and <c>SK2132</c> requires an accessor body that is a
    ///     field reference. No property can be both. This asserts that on the one file where it would
    ///     show, rather than trusting the argument.
    /// </remarks>
    [Fact]
    public void AGetOnlyPropertyWithACrossedGetter_IsOneFindingAndItIsTheAccessorRule() {
        const string source = """
                              sealed class Contact {
                                  string firstName = "";
                                  string lastName = "";

                                  public string FirstName {
                                      get => firstName;
                                      set => firstName = value;
                                  }

                                  public string LastName => firstName;

                                  string Unused => lastName;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "Contact.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id is "SK2131" or "SK2132")
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.DoesNotContain("SK2131", produced);
    }

    /// <summary>
    ///     ⚠ <c>SK2130</c> is about a field initializer and <c>SK2134</c> about a write from instance
    ///     code, and a static initializer is neither instance code nor an assignment expression — so a
    ///     type carrying both shapes produces one finding each, on different lines, and never two on
    ///     one line.
    /// </summary>
    [Fact]
    public void AStaticInitializerIsNotAnInstanceWrite() {
        const string source = """
                              sealed class Counter {
                                  static readonly int Seed = Step;
                                  static readonly int Step = 5;

                                  static int total;

                                  public Counter() => total += Seed;
                              }
                              """;

        var produced = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "Counter.cs"), Analyzers, TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Id is "SK2130" or "SK2134")
            .ToArray();

        Assert.Single(produced, static diagnostic => diagnostic.Id == "SK2130");
        Assert.Single(produced, static diagnostic => diagnostic.Id == "SK2134");
        Assert.Equal(
            2,
            produced.Select(static diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
                .Distinct()
                .Count()
        );
    }
}
