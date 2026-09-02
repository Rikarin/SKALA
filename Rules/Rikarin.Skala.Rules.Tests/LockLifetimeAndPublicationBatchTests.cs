using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Correctness;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK3060</c>–<c>SK3062</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer then produces nothing at all — so the positives fail, which
///     reads as "the rule is wrong and needs another condition", and every "should not fire" fixture
///     passes, which reads as a spotless false-positive record. The fixture harness does not look for
///     <c>AD0001</c> (issue #279), and <c>skala check</c> records it as <c>SK9030</c>, which reaches
///     only the SARIF's <c>toolExecutionNotifications</c> and does not fail the gate (issue #295). So
///     these tests do.
///     <para>
///         This batch has a specific reason to worry about it. All three rules walk <em>upwards</em>
///         from a node to find an enclosing function, an enclosing type or an enclosing constructor,
///         and a walk that assumes it will find one meets source where it does not: a top-level
///         program, a field initializer, an expression-bodied accessor, a lambda at namespace scope.
///         Every one of those returns <c>null</c> from an ancestor search that the happy path
///         dereferences.
///     </para>
/// </remarks>
public sealed class LockLifetimeAndPublicationBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new UnreleasedLockAnalyzer(), new IneffectiveLockTargetAnalyzer(),
        new ConstructorPublishesThisAnalyzer()
    ];

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
                if (fixture.RuleId is "SK3060" or "SK3061" or "SK3062") {
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
    ///     Without this, a batch whose three analyzers all returned at their first guard would report a
    ///     spotless "no <c>AD0001</c>" over fifty files, which is the exact shape of the failure the
    ///     crash test exists to catch. What is asserted is the weakest fact that cannot hold vacuously:
    ///     each of the three really does produce at least one finding across the fixture set.
    /// </remarks>
    [Fact]
    public void TheFixtureSet_ReallyReachesEveryRuleInTheBatch() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fixture in RuleFixtures.All()) {
            if (!fixture.ShouldFire || fixture.RuleId is not ("SK3060" or "SK3061" or "SK3062")) {
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

        Assert.Equal(["SK3060", "SK3061", "SK3062"], seen.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     The places an upward walk runs out of tree, in one compilation.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Each of these is source the rules
    ///     have to survive rather than source they have to judge, and pinning the verdicts here would
    ///     turn a robustness test into a second copy of the fixtures that has to be edited whenever a
    ///     boundary moves — the version of this test that quietly stops testing anything.
    /// </remarks>
    [Fact]
    public void DegenerateEnclosures_DoNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              static class NoEnclosingMethod {
                                  // A lock and an Enter reached from a field initializer's lambda: the
                                  // ancestor walk finds an anonymous function and then a field, never a method.
                                  static readonly Action Deferred = () => {
                                      var gate = new object();

                                      lock (gate) { }

                                      Monitor.Enter(gate);
                                  };
                              }

                              struct WithConstructor {
                                  public WithConstructor(int width) => Width = width;

                                  public int Width { get; }
                              }

                              record struct Pair(int Left, int Right);

                              record Positional(string Name) {
                                  public string Upper => Name.ToUpperInvariant();
                              }

                              class PrimaryOnAClass(int size) {
                                  public int Size => size;
                              }

                              sealed class ChainedInitializer {
                                  public ChainedInitializer() : this(0) { }

                                  ChainedInitializer(int seed) => Seed = seed;

                                  public int Seed { get; }
                              }

                              sealed class ExpressionBodiedConstructor {
                                  public ExpressionBodiedConstructor() => Task.Run(() => Ping());

                                  void Ping() { }
                              }

                              sealed class NestedLocker {
                                  readonly object outer = new();

                                  public void Take() {
                                      lock (outer) { }
                                  }

                                  sealed class Inner {
                                      readonly object inner = new();

                                      public void Take() {
                                          lock (inner) { }
                                      }
                                  }
                              }

                              interface IStaticAbstract {
                                  static abstract int Width { get; }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "degenerate.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ A type split across two trees, which is the shape a whole-type walk gets wrong.
    /// </summary>
    /// <remarks>
    ///     <c>SK3061</c> declares <c>scope: Compilation</c> because a partial type's `lock` statements
    ///     and the writes to its lock field can live in different files, and a symbol action is handed
    ///     several <c>DeclaringSyntaxReferences</c> whose semantic models are different objects. Asking
    ///     one tree's model about another tree's node throws, and that throw is an <c>AD0001</c> that
    ///     every negative fixture would survive.
    /// </remarks>
    [Fact]
    public void APartialTypeSplitAcrossTrees_DoesNotCrashAnAnalyzer() {
        const string first = """
                             using System.Threading;

                             partial class Split {
                                 object gate = new();

                                 public void Take() {
                                     lock (gate) {
                                         Count++;
                                     }
                                 }
                             }
                             """;
        const string second = """
                              partial class Split {
                                  public int Count { get; set; }

                                  public void Reset() => gate = new object();
                              }
                              """;

        var compilation = RuleFixtures.Compile(first, "split-one.cs");
        compilation = compilation.AddSyntaxTrees(RuleFixtures.Compile(second, "split-two.cs").SyntaxTrees);

        var diagnostics = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ <b>A <c>lock</c> in a top-level program is examined</b> (#307), and the two rules that are
    ///     still blind to one are named by measurement rather than by reading.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The synthesized <c>Program</c> type declares itself at the <c>CompilationUnitSyntax</c>,
    ///         so <c>SK3061</c>'s <c>OfType&lt;TypeDeclarationSyntax&gt;()</c> filter dropped the whole
    ///         file. ⚠ It cannot be a fixture: the harness compiles a <c>DynamicallyLinkedLibrary</c> and
    ///         top-level statements in one are <c>CS8805</c>, so the fixture would fail to compile and
    ///         prove nothing. The compilation is built here with <c>ConsoleApplication</c> instead.
    ///     </para>
    ///     <para>
    ///         ⚠ The same source is run twice, once at top level and once inside a class, and the two
    ///         results are compared rather than each asserted alone. A single assertion would pass on the
    ///         day the rule stopped firing anywhere.
    ///     </para>
    ///     <para>
    ///         ⚠ <b><c>SK3060</c> is blind to the same file and is deliberately not asserted either
    ///         way.</b> It declines through a different mechanism — its <c>Body</c> walk returns
    ///         <c>null</c> at a type declaration, which is what makes a field initializer decline too —
    ///         so pinning its silence here would turn a recorded gap into a promise. ⚠ <c>SK3044</c>,
    ///         which #307 named alongside <c>SK3061</c>, has an <em>empty</em> gap: it reports on a
    ///         field, and top-level statements declare only locals.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK3061_ExaminesALockInATopLevelProgram() {
        const string body = """
                            var gate = new object();

                            lock (gate) {
                                System.Console.WriteLine("one monitor per invocation");
                            }
                            """;

        var top = Ids(TopLevel(body));
        var inside = Ids(
            RuleFixtures.Compile("static class Runner { static void Go() { " + body + " } }", "inside.cs")
        );

        Assert.Contains("SK3061", inside);
        Assert.Contains("SK3061", top);
    }

    static string[] Ids(Compilation compilation) =>
        RuleFixtures.Analyze(
                (CSharpCompilation)compilation,
                Analyzers,
                TestContext.Current.CancellationToken
            )
            .Select(static diagnostic => diagnostic.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>The same compilation the fixture harness builds, as an executable.</summary>
    static CSharpCompilation TopLevel(string source) =>
        CSharpCompilation.Create(
            "top-level",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview), "top.cs")],
            RuleFixtures.References,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
        );

    /// <summary>
    ///     ⚠ <b>Shape A's exclusion is a static <em>field</em>, because that is all <c>SK2134</c> can
    ///     see</b> (#306).
    /// </summary>
    /// <remarks>
    ///     <c>SK3062</c> stepped aside for any static member of the constructor's own type, to keep two
    ///     findings off one line. <c>SK2134</c> (<c>instance-write-to-static</c>) binds the assignment
    ///     target and gives up on <c>is not IFieldSymbol field</c>, so a static <em>property</em> fell
    ///     through both. The two directions are asserted together and both matter: the field must stay
    ///     <c>SK2134</c>'s alone, and the property must now be <c>SK3062</c>'s alone. Asserting either
    ///     on its own would pass while the other rule had silently taken the line over.
    /// </remarks>
    [Fact]
    public void TheOwnTypesStaticProperty_IsSK3062sAlone_AndTheFieldStaysSK2134s() {
        ImmutableArray<DiagnosticAnalyzer> both = [
            new ConstructorPublishesThisAnalyzer(), new InstanceWriteToStaticAnalyzer()
        ];

        const string property = """
                                public sealed class Session {
                                    public Session(string user) {
                                        Current = this;
                                        User = user;
                                    }

                                    public static Session? Current { get; private set; }

                                    public string User { get; }
                                }
                                """;
        var onProperty = RuleFixtures.Analyze(
            RuleFixtures.Compile(property, "property.cs"),
            both,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(onProperty, static diagnostic => diagnostic.Id == "SK3062");
        Assert.DoesNotContain(onProperty, static diagnostic => diagnostic.Id == "SK2134");

        const string field = """
                             public sealed class Session {
                                 static Session? current;

                                 public Session(string user) {
                                     current = this;
                                     User = user;
                                 }

                                 public static Session? Current => current;

                                 public string User { get; }
                             }
                             """;
        var onField = RuleFixtures.Analyze(
            RuleFixtures.Compile(field, "field.cs"),
            both,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(onField, static diagnostic => diagnostic.Id == "SK2134");
        Assert.DoesNotContain(onField, static diagnostic => diagnostic.Id == "SK3062");
    }
}
