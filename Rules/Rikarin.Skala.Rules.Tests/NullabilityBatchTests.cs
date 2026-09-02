using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2110</c>–<c>SK2113</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception,
///     records it as <c>AD0001</c> and the analyzer emits nothing at all — so the positives fail,
///     which reads as "the rule is wrong", and every "should not fire" fixture passes, which reads as
///     a spotless false-positive record. The fixture harness does not check for it (issue #279) and
///     <c>skala check</c> drops it too (#295), so these tests do.
///     <para>
///         ⚠ The second thing the harness cannot ask is the one this batch turns on:
///         <b>
///             the nullable
///             context
///         </b>. Every fixture is compiled with <c>NullableContextOptions.Enable</c>, so a rule
///         whose behaviour depends on the context can only be exercised through <c>#nullable</c>
///         directives inside the file — and the compilation-level setting, which is what a real
///         project that never migrated actually has, is never seen. These tests compile the same
///         source twice, once each way.
///     </para>
/// </remarks>
public sealed class NullabilityBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ToStringReturnsNullAnalyzer(), new InertNullSuppressionAnalyzer(),
        new NullableLocalNeverNullAnalyzer(), new NullForgivenServiceResolutionAnalyzer()
    ];

    /// <summary>
    ///     Every shape that has ever crashed a nullability rule in one compilation, asserting only
    ///     that nothing threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. These are shapes the rules must
    ///     survive rather than judge — a suppression on an expression with no type, a `ToString` with
    ///     no body, a declaration whose element type does not bind — and pinning verdicts here would
    ///     make this a second copy of the fixtures that goes stale the first time a boundary moves.
    /// </remarks>
    [Fact]
    public void ADegenerateNullableShape_DoesNotCrashAnAnalyzer() {
        const string source = """
                              #nullable enable
                              namespace Fixtures {
                                  abstract class Degenerate {
                                      public abstract override string ToString();

                                      public string Field = null!;

                                      public extern void External();

                                      public void Run(string? text, int? number, System.Action action) {
                                          var a = text!;
                                          var b = number!;
                                          var c = action!;
                                          var d = (text!)?.Length;
                                          var e = default(string)!;
                                          var f = ((object?)null)!;
                                          string? g = "x", h = null;
                                          string? i = "y";
                                          i = null;
                                          System.Func<string?> j = () => null!;
                                          var k = j!;
                                          string? l = j()!;
                                      }
                                  }

                                  struct Empty {
                                      public override string ToString() => base.ToString()!;
                                  }

                                  class Generic<T> {
                                      public T? Pass(T? value) => value!;

                                      public override string? ToString() => null;
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
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    [Fact]
    public void TheDegenerateShapes_ReallyReachTheRules() {
        const string source = """
                              #nullable enable
                              namespace Fixtures {
                                  sealed class Reached {
                                      public override string? ToString() => null;
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Reached.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == RuleIds.TostringCanReturnNull);
    }

    /// <summary>
    ///     ⚠ The compilation-level nullable setting, which no fixture can express.
    /// </summary>
    /// <remarks>
    ///     A project that never migrated has <c>NullableContextOptions.Disable</c> and not a
    ///     <c>#nullable disable</c> directive, and the two reach the rules by different routes. This
    ///     compiles one source both ways and pins what each rule does, because "the rule reported
    ///     nothing" and "the rule was never asked" look identical in a report.
    /// </remarks>
    [Theory]
    [InlineData(NullableContextOptions.Enable)]
    [InlineData(NullableContextOptions.Disable)]
    public void EachRule_BehavesAsItsCatalogueEntrySaysWhenNullableIsOff(NullableContextOptions options) {
        const string source = """
                              namespace Microsoft.Extensions.DependencyInjection {
                                  static class ServiceProviderServiceExtensions {
                                      public static T GetService<T>(this System.IServiceProvider provider) => default;

                                      public static T GetRequiredService<T>(this System.IServiceProvider provider) =>
                                          throw new System.InvalidOperationException();
                                  }
                              }

                              namespace Fixtures {
                                  using Microsoft.Extensions.DependencyInjection;

                                  interface IClock { }

                                  sealed class Subject {
                                      public override string ToString() {
                                          return null;
                                      }

                                      public int Measure(string text) => text!.Length;

                                      public int Local() {
                                          string name = "a";
                                          return name.Length;
                                      }

                                      public IClock Resolve(System.IServiceProvider provider) =>
                                          provider.GetService<IClock>()!;
                                  }
                              }
                              """;

        var compilation = CSharpCompilation.Create(
            "batch",
            [
                CSharpSyntaxTree.ParseText(
                    Microsoft.CodeAnalysis.Text.SourceText.From(source),
                    new CSharpParseOptions(LanguageVersion.Preview),
                    "Subject.cs",
                    TestContext.Current.CancellationToken
                )
            ],
            RuleFixtures.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: options)
        );

        var ids = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Select(static diagnostic => diagnostic.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("AD0001", ids);

        // ⚠ The two rules that read no flow state report the same code either way. That is the claim
        // rules.json makes about them, and it is the reason they are worth having: the compilation
        // that never migrated is where CS8603 is absent and where a `!` is likeliest to be cargo.
        Assert.Contains(RuleIds.NullForgivenServiceResolution, ids);

        if (options == NullableContextOptions.Disable) {
            Assert.Contains(RuleIds.TostringCanReturnNull, ids);
            Assert.Contains(RuleIds.InertNullSuppression, ids);
        } else {
            // CS8603's ground, and SK2111's operand is a non-nullable `string` whose flow state is
            // NotNull — the half of S8969 this rule declines on purpose.
            Assert.DoesNotContain(RuleIds.TostringCanReturnNull, ids);
            Assert.DoesNotContain(RuleIds.InertNullSuppression, ids);
        }

        // ⚠ SK2112 is silent in both: the source has no `T?` local at all, because a `string?` in the
        // disabled compilation would be CS8632 and the fixture would be measuring the compiler.
        Assert.DoesNotContain(RuleIds.NullableLocalNeverNull, ids);
    }

    /// <summary>
    ///     ⚠ The withdrawal that only a compilation-level setting can show.
    /// </summary>
    /// <remarks>
    ///     <c>SK2112</c> reads a flow state, and under <c>NullableContextOptions.Disable</c> every
    ///     expression's is <see cref="NullableFlowState.None" /> — which is not <c>NotNull</c>. Without
    ///     this test the rule's silence there would be indistinguishable from the rule never having
    ///     run, which is the failure the whole batch is written against.
    /// </remarks>
    [Fact]
    public void TheLocalRule_WithdrawsFromANullableObliviousCompilation() {
        const string source = """
                              namespace Fixtures {
                                  sealed class Subject {
                                      public int Measure() {
                                          string? name = "anonymous";
                                          return name.Length;
                                      }
                                  }
                              }
                              """;

        var enabled = Analyze(source, NullableContextOptions.Enable);
        var disabled = Analyze(source, NullableContextOptions.Disable);

        Assert.Contains(RuleIds.NullableLocalNeverNull, enabled);
        Assert.DoesNotContain(RuleIds.NullableLocalNeverNull, disabled);
        Assert.DoesNotContain("AD0001", enabled);
        Assert.DoesNotContain("AD0001", disabled);
    }

    static HashSet<string> Analyze(string source, NullableContextOptions options) {
        var compilation = CSharpCompilation.Create(
            "batch",
            [
                CSharpSyntaxTree.ParseText(
                    Microsoft.CodeAnalysis.Text.SourceText.From(source),
                    new CSharpParseOptions(LanguageVersion.Preview),
                    "Subject.cs",
                    TestContext.Current.CancellationToken
                )
            ],
            RuleFixtures.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: options)
        );

        return RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken)
            .Select(static diagnostic => diagnostic.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
