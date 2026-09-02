using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Cleanup;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2140</c>–<c>SK2143</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c> and the analyzer then produces nothing at all — so the positives fail, which
///     reads as "the rule is wrong", and every "should not fire" fixture passes, which reads as a clean
///     false-positive record. The fixture harness does not check for it (issue #279) and
///     <c>skala check</c> drops <c>AD0001</c> outright (issue #295), so it is invisible in production
///     too.
///     <para>
///         ⚠ These four rules walk parameter lists against argument lists, which is the exact shape
///         that has already failed here once: <c>SK0232</c> throws <c>IndexOutOfRangeException</c> on
///         every expanded <c>params</c> call (issue #298) because its loop bounds the counter and
///         indexes with the argument position. This file is the standing check that the same mistake
///         has not been made a second time.
///     </para>
/// </remarks>
public sealed class ParameterContractBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new OverriddenParameterDefaultAnalyzer(), new RestatedCallerInfoArgumentAnalyzer(),
        new OverwrittenParameterAnalyzer(), new CrosswiseArgumentOrderAnalyzer()
    ];

    /// <summary>
    ///     Every call shape where the argument list and the parameter list do not line up, in one
    ///     compilation, asserting only that nothing threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately not about what is reported. Each of these is a shape the
    ///     rules have to survive rather than one they have to judge; pinning verdicts here would make
    ///     this a second copy of the fixtures that has to be edited whenever a boundary moves, which is
    ///     the version of this test that quietly stops testing anything.
    /// </remarks>
    [Fact]
    public void AMisalignedArgumentList_DoesNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Runtime.CompilerServices;

                              namespace Fixtures {
                                  static class Extensions {
                                      public static void Tag(this string value, string name, int count = 0) { }

                                      public static void Spread(this string value, params int[] rest) { }
                                  }

                                  sealed class Misaligned {
                                      public string this[string source, string destination] => source;

                                      public void Run(string source, string destination) {
                                          // More arguments than parameters: SK0232's crash (#298).
                                          Take(1, 2, 3);
                                          Take(1);
                                          Take(1, 2);
                                          Restate("a", nameof(Run), 1, 2, 3);

                                          // Fewer arguments than parameters.
                                          Optional("a");
                                          Optional();

                                          // Named arguments in every position, including out of order.
                                          Optional(second: 2, first: "a");
                                          Optional("a", second: 2);
                                          Restate(message: "a", member: nameof(Run));

                                          // Byref arguments, including an inline declaration.
                                          int written;
                                          ByRef(out written, ref written);
                                          ByRef(out var inline, ref written);
                                          Console.WriteLine(inline + written);

                                          // Reduced and unreduced extension calls.
                                          source.Tag(destination);
                                          Extensions.Tag(source, destination);
                                          source.Spread(1, 2, 3);
                                          Extensions.Spread(source, 1, 2, 3);

                                          // Delegate invocation, generic inference, indexer, constructor.
                                          Action<string, string> del = (a, b) => { };
                                          del(destination, source);
                                          Generic(destination, source);
                                          Console.WriteLine(this[destination, source]);
                                          _ = new Pair(destination, source);
                                          _ = new Pair();

                                          // A caller-info attribute naming a parameter that is not there,
                                          // and one whose argument is omitted.
                                          Dangling("a");
                                          Dangling("a", "b");
                                          Missing("a");
                                      }

                                      static void Take(int first, params int[] rest) { }

                                      static void Optional(string first = "", int second = 0) { }

                                      static void ByRef(out int written, ref int updated) {
                                          written = updated;
                                      }

                                      static void Generic<T>(T source, T destination) { }

                                      static void Restate(
                                          string message,
                                          [CallerMemberName] string? member = null,
                                          params int[] rest
                                      ) { }

                                      static void Dangling(
                                          string first,
                                          [CallerArgumentExpression("nosuchparameter")] string? expression = null
                                      ) { }

                                      static void Missing(
                                          string first,
                                          string? second = null,
                                          [CallerArgumentExpression("second")] string? expression = null
                                      ) { }
                                  }

                                  sealed class Pair {
                                      public Pair() { }

                                      public Pair(string source, string destination) { }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Misaligned.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer that never registers also never crashes.
    /// </summary>
    /// <remarks>
    ///     Every rule here is registered on a syntax kind rather than behind a
    ///     <c>RegisterCompilationStartAction</c> type lookup, so nothing can switch them all off — but
    ///     "no <c>AD0001</c>" would still be a fact about the harness rather than about the rules if
    ///     the source never reached them. This asserts all four really do fire on one compilation.
    /// </remarks>
    [Fact]
    public void TheMisalignedShapes_ReallyReachTheRules() {
        const string source = """
                              using System.Runtime.CompilerServices;

                              namespace Fixtures {
                                  interface IPlain {
                                      void Accept(string name, params int[] values);
                                  }

                                  sealed class Reached : IPlain {
                                      // SK2140: the implementation drops the interface's `params`.
                                      public void Accept(string name, int[] values) { }

                                      public void Run(string source, string destination) {
                                          // SK2141: the member name is restated exactly.
                                          Trace("started", nameof(Run));

                                          // SK2143: two adjacent same-typed arguments, crosswise.
                                          Copy(destination, source);
                                      }

                                      // SK2142: the caller's value is replaced before anything reads it.
                                      public void Render(string path) {
                                          path = "default.txt";
                                          System.Console.WriteLine(path);
                                      }

                                      static void Trace(string message, [CallerMemberName] string? member = null) { }

                                      static void Copy(string source, string destination) { }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Reached.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.OverriddenParameterDefault);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.RestatedCallerInfoArgument);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.OverwrittenParameter);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.CrosswiseArgumentOrder);
    }

    /// <summary>
    ///     ⚠ The span <c>SK2141</c> and <c>SK0232</c> both look at, asserted from both sides at once.
    /// </summary>
    /// <remarks>
    ///     The two rules must never report the same argument, and the reason they cannot is structural:
    ///     <c>SK0232</c> excludes caller-info parameters from its scan outright, and <c>SK2141</c>
    ///     reports only a restatement of the substitution or a fabricated location — never <c>null</c>,
    ///     which is the case <c>SK0232</c>'s own false-positive note records as the opposite of
    ///     redundant. The fixtures pin each half separately; this runs both analyzers over one file so
    ///     that a change to either one which broke the disjointness fails here.
    ///     <para>
    ///         ⚠ <c>SK0232</c> is included for a second reason. It throws on every expanded
    ///         <c>params</c> call (#298), so it is a live example of the failure this whole file
    ///         guards against — and the source below is deliberately free of that shape, so this test
    ///         measures the overlap rather than re-reporting the known crash.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SK2141AndSK0232_NeverReportTheSameArgument() {
        const string source = """
                              using System.Runtime.CompilerServices;

                              namespace Fixtures {
                                  sealed class Overlap {
                                      public void Run() {
                                          // SK2141's finding: the substitution restated.
                                          Trace("a", nameof(Run));

                                          // SK0232's finding: an ordinary default restated.
                                          Plain("a", false);

                                          // Neither: SK0232 declines caller-info parameters, and this
                                          // argument is the only thing keeping the value null.
                                          Trace("a", null);
                                      }

                                      static void Trace(string message, [CallerMemberName] string? member = null) { }

                                      static void Plain(string message, bool verbose = false) { }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Overlap.cs"),
            [new RestatedCallerInfoArgumentAnalyzer(), new RedundantArgumentAnalyzer()],
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");

        var mine = diagnostics.Where(static d => d.Id == RuleIds.RestatedCallerInfoArgument).ToArray();
        var theirs = diagnostics.Where(static d => d.Id == RuleIds.RedundantArgument).ToArray();

        // Anti-vacuity: two disjoint empty sets are also disjoint.
        Assert.Single(mine);
        Assert.Single(theirs);

        foreach (var one in mine) {
            foreach (var other in theirs) {
                Assert.False(
                    one.Location.SourceSpan.IntersectsWith(other.Location.SourceSpan),
                    $"SK2141 at {one.Location.GetLineSpan()} and SK0232 at {other.Location.GetLineSpan()} "
                    + "overlap. The two rules are meant to be disjoint by construction: SK0232 excludes "
                    + "caller-info parameters and SK2141 reports nothing else."
                );
            }
        }
    }
}
