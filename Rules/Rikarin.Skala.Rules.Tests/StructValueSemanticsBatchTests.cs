using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2190</c>–<c>SK2194</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception,
///     reports it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives
///     fail, which reads as "the rule is wrong", and every "should not fire" fixture passes, which
///     reads as a clean false-positive record. The fixture harness does not check for it (issue #279)
///     and <c>skala check</c> records it only into the SARIF's <c>toolExecutionNotifications</c>
///     (issue #295), so it is nearly invisible in production too.
///     <para>
///         ⚠ These five rules read struct members, generic type arguments, bound operators and
///         primary constructor parameter lists — every one of them a place where a shape the analysis
///         did not expect is an unguarded index or an unguarded cast rather than a wrong answer.
///     </para>
/// </remarks>
public sealed class StructValueSemanticsBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new StructKeyWithoutEqualityAnalyzer(), new ReadonlyReceiverMutationAnalyzer(),
        new SpanReferenceComparisonAnalyzer(), new ImmutableArrayCollectionInitializerAnalyzer(),
        new MutableCapturedPrimaryParameterAnalyzer()
    ];

    /// <summary>
    ///     Every awkward struct, span and primary-constructor shape in one compilation, asserting only
    ///     that nothing threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Each of these is a shape the
    ///     rules have to survive rather than one they have to judge; pinning verdicts here would make
    ///     this a second copy of the fixtures that has to be edited whenever a boundary moves, which is
    ///     the version of this test that quietly stops testing anything.
    /// </remarks>
    [Fact]
    public void AwkwardValueTypeShapes_DoNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Collections.Generic;
                              using System.Collections.Immutable;

                              namespace Fixtures {
                                  ref struct Window {
                                      public ReadOnlySpan<char> Text;
                                  }

                                  struct Open<T> {
                                      public T Value;

                                      public void Set(T value) => Value = value;
                                  }

                                  unsafe struct Raw {
                                      public fixed byte Bytes[8];
                                  }

                                  struct Recursive {
                                      public Recursive[] Children;

                                      public void Grow() => Children = new Recursive[1];
                                  }

                                  interface IMarker {
                                      void Reset();
                                  }

                                  struct Explicit : IMarker {
                                      public int Value;

                                      void IMarker.Reset() => Value = 0;
                                  }

                                  sealed class Odd<TKey>(TKey key, int count) where TKey : struct {
                                      // A generic key the analysis cannot resolve to a concrete struct.
                                      readonly Dictionary<TKey, int> byKey = new Dictionary<TKey, int>();

                                      // An unbound and an error-typed argument list.
                                      readonly Dictionary<Open<int>, int> nested = new Dictionary<Open<int>, int>();

                                      readonly HashSet<Recursive> cyclic = new HashSet<Recursive>();

                                      public int Count => byKey.Count + nested.Count + cyclic.Count + count;

                                      public TKey Key => key;

                                      public void Bump() {
                                          count++;
                                      }
                                  }

                                  static class Shapes {
                                      public static bool Nested(ReadOnlySpan<char> value) =>
                                          (value == default) == (value != default);

                                      public static bool Chained(ReadOnlySpan<char> a, ReadOnlySpan<char> b) =>
                                          a == b == true;

                                      public static bool Conditional(string? text, ReadOnlySpan<char> value) =>
                                          (text is null ? value : value) == value;

                                      public static void Copies(in Open<int> open, in Raw raw, in Explicit marker) {
                                          open.Set(1);
                                          _ = raw;
                                          _ = marker;
                                      }

                                      public static void Loops(Open<int>[] values, Span<Open<int>> span) {
                                          foreach (var value in values) {
                                              value.Set(1);
                                          }

                                          foreach (ref var value in span) {
                                              value.Set(1);
                                          }
                                      }

                                      public static ImmutableArray<int> Arrays() {
                                          var jagged = new ImmutableArray<ImmutableArray<int>> {
                                              ImmutableArray<int>.Empty
                                          };
                                          _ = jagged;
                                          _ = new ImmutableArray<int>();
                                          return new ImmutableArray<int> { 1 };
                                      }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Awkward.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    const string AllFive = """
                           using System;
                           using System.Collections.Generic;
                           using System.Collections.Immutable;

                           namespace Fixtures {
                               struct Counter {
                                   public int Value;

                                   public void Increment() => Value++;
                               }

                               sealed class Reached(int budget) {
                                   // SK2190: a hash-based collection over a struct that declares nothing.
                                   readonly Dictionary<Counter, string> labels = new Dictionary<Counter, string>();

                                   // SK2191: the write goes to the copy made for the `in` parameter.
                                   public static void Bump(in Counter counter) => counter.Increment();

                                   // SK2192: the comparison is about memory.
                                   public static bool Same(ReadOnlySpan<char> a, ReadOnlySpan<char> b) => a == b;

                                   // SK2193: the collection initializer calls Add on the default struct.
                                   public static ImmutableArray<int> Ids() => new ImmutableArray<int> { 1, 2 };

                                   // SK2194: the captured parameter is assigned.
                                   public int Spend() {
                                       budget--;
                                       return budget;
                                   }

                                   public int Size => labels.Count;
                               }
                           }
                           """;

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer that never registers also never crashes.
    /// </summary>
    /// <remarks>
    ///     <c>SK2190</c> is behind a <c>RegisterCompilationStartAction</c> type lookup, so a mistyped
    ///     metadata name would switch it off entirely and leave "no <c>AD0001</c>" a true statement
    ///     about nothing. This asserts all five really fire on one compilation.
    /// </remarks>
    [Fact]
    public void EveryRuleInTheBatch_FiresOnOneCompilation() {
        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(AllFive, "AllFive.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        foreach (var id in new[] {
                     RuleIds.StructKeyWithoutEquality, RuleIds.ReadonlyReceiverMutation,
                     RuleIds.SpanReferenceComparison, RuleIds.ImmutableArrayCollectionInitializer,
                     RuleIds.MutableCapturedPrimaryParameter
                 }) {
            Assert.True(
                diagnostics.Any(diagnostic => diagnostic.Id == id),
                id
                + " did not fire on a compilation written to contain its shape, so every other "
                + "assertion in this file is about an analyzer that is not running."
            );
        }
    }

    /// <summary>
    ///     ⚠ <c>SK2005</c> and <c>SK2191</c> partition the receivers between them, and this is that
    ///     claim on the one file where it would show.
    /// </summary>
    /// <remarks>
    ///     The two rules report the same lost write. <c>SK2005</c> takes the <c>readonly</c> field
    ///     receiver; <c>SK2191</c> takes the <c>in</c> parameter, the <c>ref readonly</c> local and the
    ///     <c>foreach</c> variable. Arguing the partition in prose is free; running both analyzers over
    ///     one file that holds every receiver, and counting, is not.
    /// </remarks>
    [Fact]
    public void ReadonlyFieldsBelongToSk2005AndTheOtherReceiversToSk2191() {
        const string source = """
                              namespace Fixtures {
                                  struct Counter {
                                      public int Value;

                                      public void Increment() => Value++;
                                  }

                                  sealed class Every {
                                      readonly Counter field;

                                      public void ThroughAField() => field.Increment();

                                      public static void ThroughAnInParameter(in Counter counter) =>
                                          counter.Increment();

                                      public static void ThroughAForeachVariable(Counter[] counters) {
                                          foreach (var counter in counters) {
                                              counter.Increment();
                                          }
                                      }

                                      public static void ThroughARefReadonlyLocal(Counter[] counters) {
                                          ref readonly var counter = ref counters[0];
                                          counter.Increment();
                                      }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Every.cs"),
            [new ReadonlyStructMutationAnalyzer(), new ReadonlyReceiverMutationAnalyzer()],
            TestContext.Current.CancellationToken
        );

        var sk2005 = diagnostics.Where(static d => d.Id == RuleIds.ReadonlyStructMutation).ToArray();
        var sk2191 = diagnostics.Where(static d => d.Id == RuleIds.ReadonlyReceiverMutation).ToArray();

        Assert.Single(sk2005);
        Assert.Equal(3, sk2191.Length);

        // ⚠ The partition is what matters, not the counts: no span may carry both findings.
        Assert.Empty(
            sk2005.Select(static d => d.Location.SourceSpan)
                .Intersect(sk2191.Select(static d => d.Location.SourceSpan))
        );
    }

    /// <summary>
    ///     ⚠ <c>SK2011</c> reports the <c>.Equals</c> call and <c>SK2190</c> reports the collection,
    ///     and issue #4 said otherwise.
    /// </summary>
    /// <remarks>
    ///     The issue's premise is that <c>SK2011</c> reports at the struct's <em>declaration</em>, which
    ///     would have made <c>SK2190</c> a duplicate of it. <c>InheritedValueTypeEqualsAnalyzer</c>
    ///     registers on <c>InvocationExpression</c>. This pins the refutation to a run rather than to a
    ///     reading: on one struct with no equality, the two rules land on two different spans and
    ///     neither lands on the declaration.
    /// </remarks>
    [Fact]
    public void Sk2011ReportsTheCallAndSk2190ReportsTheCollection() {
        const string source = """
                              using System.Collections.Generic;

                              namespace Fixtures {
                                  struct Cell {
                                      public int Row;
                                  }

                                  sealed class Board {
                                      readonly Dictionary<Cell, string> labels = new Dictionary<Cell, string>();

                                      public bool Same(Cell left, Cell right) => left.Equals(right);

                                      public int Size => labels.Count;
                                  }
                              }
                              """;

        var compilation = RuleFixtures.Compile(source, "Board.cs");
        var diagnostics = RuleFixtures.Analyze(
            compilation,
            [new InheritedValueTypeEqualsAnalyzer(), new StructKeyWithoutEqualityAnalyzer()],
            TestContext.Current.CancellationToken
        );

        var text = compilation.SyntaxTrees.Single().GetText(TestContext.Current.CancellationToken);
        var sk2011 = Assert.Single(diagnostics.Where(static d => d.Id == RuleIds.InheritedValueTypeEquals));
        var sk2190 = Assert.Single(diagnostics.Where(static d => d.Id == RuleIds.StructKeyWithoutEquality));

        Assert.Equal("left.Equals(right)", text.ToString(sk2011.Location.SourceSpan));
        Assert.Equal("new Dictionary<Cell, string>()", text.ToString(sk2190.Location.SourceSpan));
    }

    /// <summary>
    ///     ⚠ The shapes three of these issues asserted would not compile, compiled here.
    /// </summary>
    /// <remarks>
    ///     A probe outside the repository is what established it, and a probe is not something the
    ///     build re-runs. This is the same claim as a compilation the test suite owns: if a framework
    ///     update ever removes <c>ReadOnlySpan&lt;T&gt;</c>'s operators or makes
    ///     <c>new ImmutableArray&lt;T&gt; { … }</c> an error, three rules become dead code and this is
    ///     what says so.
    /// </remarks>
    [Fact]
    public void TheShapesTheIssuesDoubted_ActuallyCompile() {
        const string source = """
                              using System;
                              using System.Collections.Immutable;

                              namespace Fixtures {
                                  static class Doubted {
                                      public static bool ReadOnlySpans(ReadOnlySpan<char> a, ReadOnlySpan<char> b) =>
                                          a == b;

                                      public static bool Spans(Span<char> a, Span<char> b) => a == b;

                                      public static bool Bytes(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b) => a != b;

                                      public static bool AgainstAString(ReadOnlySpan<char> a, string b) => a == b;

                                      public static ImmutableArray<int> Initializer() => new ImmutableArray<int> { 1 };

                                      public static int Captured() => new Held(1).Next();
                                  }

                                  sealed class Held(int count) {
                                      public int Next() {
                                          count--;
                                          return count;
                                      }
                                  }
                              }
                              """;

        var errors = RuleFixtures.Compile(source, "Doubted.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "A shape three of these rules depend on stopped compiling:\n  "
            + string.Join("\n  ", errors.Select(static d => d.ToString()))
        );
    }

    /// <summary>
    ///     ⚠ <c>span.Equals(span)</c> does <b>not</b> compile, and the issue proposed it as the shape
    ///     to report.
    /// </summary>
    /// <remarks>
    ///     The only <c>Equals</c> in reach on a span takes <c>object</c>, and a byref-like type cannot
    ///     be boxed, so this is <c>CS1503</c> rather than a misleading call. It is pinned here because
    ///     the day it starts compiling is the day <c>SK2192</c> has a second half to grow.
    /// </remarks>
    [Fact]
    public void SpanEquals_DoesNotCompileAtAll() {
        const string source = """
                              using System;

                              namespace Fixtures {
                                  static class Doubted {
                                      public static bool Same(ReadOnlySpan<char> a, ReadOnlySpan<char> b) =>
                                          a.Equals(b);
                                  }
                              }
                              """;

        var errors = RuleFixtures.Compile(source, "SpanEquals.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Contains(errors, static d => d.Id == "CS1503");
    }

    /// <summary>
    ///     ⚠ <c>SK2193</c>'s fix is bound, not just parsed, and the qualifier it reuses is the one the
    ///     source wrote.
    /// </summary>
    /// <remarks>
    ///     <see cref="RuleFixtureTests.EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic" /> already
    ///     re-binds every fix. What it cannot say is *which* replacement was produced, and the whole
    ///     design of this one is that a fully qualified `new` keeps its qualifier while an imported one
    ///     does not acquire one.
    /// </remarks>
    [Fact]
    public void TheImmutableArrayFix_KeepsTheSpellingTheSourceUsed() {
        const string source = """
                              using System.Collections.Immutable;

                              namespace Fixtures {
                                  static class Both {
                                      public static ImmutableArray<int> Short() => new ImmutableArray<int> { 1, 2 };

                                      public static ImmutableArray<string> Long() =>
                                          new System.Collections.Immutable.ImmutableArray<string> { "a" };
                                  }
                              }
                              """;

        var replacements = RuleFixtures
            .Analyze(
                RuleFixtures.Compile(source, "Both.cs"),
                [new ImmutableArrayCollectionInitializerAnalyzer()],
                TestContext.Current.CancellationToken
            )
            .Where(static d => d.Id == RuleIds.ImmutableArrayCollectionInitializer)
            .Select(static d => d.Properties[FixEdits.TextKey(0)])
            .ToArray();

        Assert.Equal(2, replacements.Length);
        Assert.Contains("ImmutableArray.Create<int>(1, 2)", replacements);
        Assert.Contains("System.Collections.Immutable.ImmutableArray.Create<string>(\"a\")", replacements);
    }
}
