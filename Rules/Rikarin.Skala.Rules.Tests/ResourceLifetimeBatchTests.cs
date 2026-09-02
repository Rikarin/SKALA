using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK3540</c>–<c>SK3542</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer produces nothing at all afterwards — so the positives fail,
///     which reads as "the predicate needs another condition", and every "should not fire" fixture
///     passes, which reads as a spotless false-positive record. The shared fixture harness does not look
///     for <c>AD0001</c> (issue #279) and <c>skala check</c> drops it into a notification that fails no
///     gate (issues #295, #298), so these tests do.
///     <para>
///         This batch has a specific reason to worry about it: all three rules resolve a framework type
///         at compilation start and then walk symbols that are null or in error in ordinary broken
///         source — a base type that does not resolve, a receiver with no type, a <c>Dispose</c> on a
///         type the model could not bind.
///     </para>
///     <para>
///         ⚠ The other thing asserted here and nowhere else is disjointness from the rest of the
///         family. <c>SK3540</c> is deliberately not made disjoint from <c>SK3502</c> — the two read
///         different declarations and can both be right about one type — and that is a decision rather
///         than an oversight, so it is pinned from both directions.
///     </para>
/// </remarks>
public sealed class ResourceLifetimeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new UndeclaredDisposeAnalyzer(), new ShortLivedHttpClientAnalyzer(), new DangerousHandleAnalyzer()
    ];

    /// <summary>
    ///     ⚠ The family this batch joins, for the two disjointness assertions below.
    /// </summary>
    static readonly ImmutableArray<DiagnosticAnalyzer> WithTheFamily = [
        new UndeclaredDisposeAnalyzer(), new ShortLivedHttpClientAnalyzer(), new DangerousHandleAnalyzer(),
        new OwnedDisposableFieldAnalyzer(), new RefStructOwnedDisposableAnalyzer()
    ];

    static readonly string[] Batch = ["SK3540", "SK3541", "SK3542"];

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

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    /// <remarks>
    ///     Without this, three analyzers all returning at their first guard would produce a spotless
    ///     "no <c>AD0001</c>" across the whole fixture set — which is the exact shape of the failure the
    ///     crash test exists to catch. What is asserted is the weakest fact that cannot hold vacuously:
    ///     each of the three really does produce a finding somewhere in the set.
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
    ///     One finding per positive fixture, none per negative, and the fix exactly where declared.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureRecords))]
    public void Fixtures_HaveExactCountsAndCarryTheirFix(RuleFixture fixture) {
        var findings = RuleFixtures
            .Analyze(
                RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path),
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);

        // ⚠ Both directions. A rule the catalogue calls fixless must not smuggle edits into the
        // property bag either: `skala fix --safe` reads the bag rather than `hasFix`, so a stray
        // edit on a rule declared fixless is applied without anything having decided it was safe.
        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    public static TheoryData<RuleFixture> FixtureRecords {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()) {
                if (Batch.Contains(fixture.RuleId, StringComparer.Ordinal)) {
                    data.Add(fixture);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     ⚠ The fix's three insertion points, compared as text.
    /// </summary>
    /// <remarks>
    ///     <c>RuleFixtureTests</c> asks whether the result parses and re-binds, and both questions pass
    ///     for an edit that lands in the wrong place as long as the wrong place is still legal. The
    ///     generic case is the one that matters: a base list must precede the constraint clauses, so
    ///     putting <c>IDisposable</c> after <c>where T : struct</c> is not a program at all. Only
    ///     comparing the text says which of the two was written.
    /// </remarks>
    [Fact]
    public void TheFixGoesBeforeTheConstraintsAndAfterTheBaseList() {
        const string source = """
                              using System;
                              using System.IO;

                              public abstract class Store { }

                              public sealed class Plain {
                                  readonly MemoryStream a = new();

                                  public void Dispose() {
                                      a.Dispose();
                                  }
                              }

                              public sealed class Derived : Store {
                                  readonly MemoryStream b = new();

                                  public void Dispose() {
                                      b.Dispose();
                                  }
                              }

                              public sealed class Constrained<T> where T : struct {
                                  readonly MemoryStream c = new();

                                  public void Dispose() {
                                      c.Dispose();
                                  }
                              }
                              """;

        Assert.Equal(
            [
                "public sealed class Plain : IDisposable {",
                "public sealed class Derived : Store, IDisposable {",
                "public sealed class Constrained<T> : IDisposable where T : struct {"
            ],
            Apply(source)
                .Split('\n')
                .Where(static line => line.Contains("IDisposable", StringComparison.Ordinal))
                .Select(static line => line.TrimEnd('\r'))
                .ToArray()
        );
    }

    /// <summary>
    ///     ⚠ <c>System</c> is not always imported, and a fix that names <c>IDisposable</c> unqualified
    ///     in a file that never imported it is text that parses and does not compile.
    /// </summary>
    [Fact]
    public void WithoutTheImport_TheFixQualifiesTheName() {
        const string source = """
                              public sealed class Journal {
                                  readonly System.IO.MemoryStream file = new();

                                  public void Dispose() {
                                      file.Dispose();
                                  }
                              }
                              """;

        Assert.Contains("class Journal : System.IDisposable {", Apply(source), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>SK3540</c> and <c>SK3502</c> are <b>not</b> disjoint, and that is the decision.
    /// </summary>
    /// <remarks>
    ///     The two read different declarations — <c>SK3502</c> a field that is constructed and not
    ///     matched by a contract, <c>SK3540</c> a method that is written and not declared — and a type
    ///     can be wrong in both ways at once. Suppressing either would delete a true statement about
    ///     the type, and <c>supersedes</c> is the wrong instrument in any case, since
    ///     <c>Supersession.Apply</c> works on a shared span and these two report different spans. This
    ///     pins the choice so that a later "deduplication" has to argue with a test.
    /// </remarks>
    [Fact]
    public void ATypeCanBeWrongInBothWays_AndBothRulesSayTheirOwnHalf() {
        const string source = """
                              using System.IO;

                              public sealed class Journal {
                                  readonly MemoryStream file = new();

                                  public void Dispose() {
                                      file.Dispose();
                                  }
                              }
                              """;

        var ids = RuleFixtures
            .Analyze(RuleFixtures.Compile(source, "both.cs"), WithTheFamily, TestContext.Current.CancellationToken)
            .Select(static d => d.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["SK3502", "SK3540"], ids);
    }

    /// <summary>
    ///     ⚠ <c>SK3540</c> and <c>SK3532</c> <b>are</b> disjoint, by the <c>ref struct</c> test.
    /// </summary>
    /// <remarks>
    ///     A <c>ref struct</c>'s public parameterless <c>Dispose()</c> is the disposal contract — the
    ///     language's pattern rule binds <c>using</c> to it with no interface anywhere — so reporting it
    ///     as undeclared would report the correct spelling of the thing as a defect, and would do it on
    ///     exactly the declaration <c>SK3532</c> exists to say is <em>missing</em>. The two would then
    ///     contradict each other on one type.
    /// </remarks>
    [Fact]
    public void ARefStructsPatternDispose_IsSk3532sSubjectAndNotThisRules() {
        const string source = """
                              using System.IO;

                              public ref struct Window {
                                  readonly MemoryStream buffer;

                                  public Window(MemoryStream target) {
                                      buffer = target;
                                  }

                                  public void Dispose() {
                                      buffer.Dispose();
                                  }
                              }
                              """;

        Assert.Empty(
            RuleFixtures.Analyze(
                RuleFixtures.Compile(source, "pattern.cs"),
                WithTheFamily,
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>
    ///     Source the rules have to survive rather than source they have to judge.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Pinning verdicts here would turn
    ///     a robustness test into a second copy of the fixtures that has to be edited whenever a
    ///     boundary moves — the version of this test that quietly stops testing anything. Every one of
    ///     these shapes puts a null or an error symbol in front of a dereference the three rules make:
    ///     an unresolved base type, a receiver with no type at all, a <c>using</c> over something that
    ///     does not exist, and a <c>DangerousGetHandle</c> outside any type declaration.
    /// </remarks>
    [Fact]
    public void BrokenAndExoticSource_DoesNotCrashAnAnalyzer() {
        const string source = """
                              public sealed class Orphan : ThisTypeDoesNotExist {
                                  public void Dispose() {
                                      Missing.Dispose();
                                  }
                              }

                              public interface IStore {
                                  void Dispose();
                              }

                              public unsafe struct Raw {
                                  public void Dispose() {
                                      Nothing().Close();
                                  }
                              }

                              public static class Loose {
                                  public static void Go() {
                                      using var client = new NotAClient();
                                      using (Unknown()) { }
                                      var raw = Undefined.DangerousGetHandle();
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "broken.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    static string Apply(string source) {
        var text = source;
        var edits = new List<(int Start, int Length, string Text)>();
        foreach (var diagnostic in RuleFixtures
                     .Analyze(RuleFixtures.Compile(source, "fix.cs"), Analyzers, TestContext.Current.CancellationToken)
                     .Where(static d => d.Id == "SK3540")) {
            var count = int.Parse(
                diagnostic.Properties[FixEdits.CountKey]!,
                System.Globalization.CultureInfo.InvariantCulture
            );

            for (var i = 0; i < count; i++) {
                edits.Add(
                    (
                        int.Parse(
                            diagnostic.Properties[FixEdits.StartKey(i)]!,
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        int.Parse(
                            diagnostic.Properties[FixEdits.LengthKey(i)]!,
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        diagnostic.Properties[FixEdits.TextKey(i)]!
                    )
                );
            }
        }

        Assert.NotEmpty(edits);
        foreach (var (start, length, replacement) in edits.OrderByDescending(static edit => edit.Start)) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        return text;
    }
}
