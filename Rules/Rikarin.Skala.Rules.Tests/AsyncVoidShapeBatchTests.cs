using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK3050</c>–<c>SK3052</c>: what the fixture harness cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer produces nothing at all — so the positives fail, which
///     reads as "the rule is wrong", and every single "should not fire" fixture passes, which reads as
///     a clean false-positive record. The fixture harness does not check for it (issue #279), so these
///     tests do, over the inputs most likely to cause one: three rules that walk *up* a syntax tree
///     with a stopping condition, where the crash is the walk that runs out of tree.
///     <para>
///         ⚠ <b>The disjointness tests below run on one file that satisfies both rules at once</b>, and
///         that is the whole point of them. A pair of files that differ in shape proves the shapes
///         differ, which is true whether or not either rule looks — the failure mode a rule in this band
///         shipped with this week. Every pair here is one source and one edit to it.
///     </para>
/// </remarks>
public sealed class AsyncVoidShapeBatchTests {
    /// <summary>
    ///     ⚠ <c>SK3001</c>, <c>SK3004</c> and <c>SK3005</c> are in the set on purpose: each of the three
    ///     new rules is disjoint from one of them by construction, and asserting that needs both running.
    /// </summary>
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new AsyncVoidThrowAnalyzer(), new UncancellableAsyncMethodAnalyzer(), new AsyncVoidLambdaAnalyzer(),
        new AsyncVoidAnalyzer(), new CancellationTokenForwardingAnalyzer(), new FireAndForgetTaskAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All()
                         .Where(static f => f.RuleId is "SK3050" or "SK3051" or "SK3052")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndCarryTheirFix(RuleFixture fixture) {
        var findings = Analyze(File.ReadAllText(fixture.Path), fixture.Path)
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);

        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>
    ///     ⚠ One source that is an <c>async void</c> body <em>and</em> contains a <c>throw</c>.
    /// </summary>
    /// <remarks>
    ///     Both rules' subject matter is present, so this is a test of which one claims it rather than a
    ///     test that two shapes differ. The lambda owns it: the remedy is at the conversion, and
    ///     <c>SK3050</c> would have nothing to say that <c>SK3052</c> does not already say better.
    ///     <para>
    ///         ⚠ The second half is the anti-vacuity, and it is the same file with the lambda written out
    ///         as a declaration. Without it, "SK3050 did not fire" is equally consistent with SK3050
    ///         never having run.
    ///     </para>
    /// </remarks>
    [Fact]
    public void AnAsyncVoidBodyThatThrows_IsClaimedByTheRuleThatOwnsTheOwner() {
        const string lambda = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class Wiring {
                                  public void Run(Action callback) => callback();

                                  public void Wire() {
                                      Run(async () => {
                                          await Task.Yield();
                                          throw new InvalidOperationException("unobservable");
                                      });
                                  }
                              }
                              """;

        var fromLambda = Analyze(lambda, "Wiring.cs");
        Assert.Single(fromLambda, static d => d.Id == RuleIds.AsyncVoidLambda);
        Assert.DoesNotContain(fromLambda, static d => d.Id == RuleIds.AsyncVoidThrow);

        // The same body, written as the declaration the lambda was hiding.
        var declaration = lambda
            .Replace("public void Run(Action callback) => callback();", "", StringComparison.Ordinal)
            .Replace(
                "public void Wire() {\n        Run(async () => {",
                "public async void Wire() {",
                StringComparison.Ordinal
            )
            .Replace("        });\n    }", "    }", StringComparison.Ordinal);

        var fromDeclaration = Analyze(declaration, "Wiring.cs");
        Assert.Single(fromDeclaration, static d => d.Id == RuleIds.AsyncVoidThrow);
        Assert.DoesNotContain(fromDeclaration, static d => d.Id == RuleIds.AsyncVoidLambda);
    }

    /// <summary>
    ///     ⚠ One source carrying <c>SK3005</c>'s exact shape — a bare task-producing statement — inside
    ///     the lambda <c>SK3052</c> reports.
    /// </summary>
    /// <remarks>
    ///     <c>SK3005</c> declines because the nearest owner is <c>async</c>, which is the construction
    ///     that makes the two disjoint. Deleting one keyword moves the same file from one rule to the
    ///     other, and that is what proves both were looking.
    /// </remarks>
    [Fact]
    public void ADiscardedTaskInsideAnAsyncLambda_IsSk3052sAndNotSk3005s() {
        const string source = """
                              using System;
                              using System.Threading.Tasks;

                              public sealed class Wiring {
                                  public void Run(Action callback) => callback();

                                  public void Wire() {
                                      Run(async () => {
                                          await Task.Yield();
                                          FlushAsync();
                                      });
                                  }

                                  static Task FlushAsync() => Task.CompletedTask;
                              }
                              """;

        var asynchronous = Analyze(source, "Wiring.cs");
        Assert.Single(asynchronous, static d => d.Id == RuleIds.AsyncVoidLambda);
        Assert.DoesNotContain(asynchronous, static d => d.Id == RuleIds.FireAndForgetTask);

        var synchronous = source
            .Replace("Run(async () => {\n            await Task.Yield();\n", "Run(() => {\n", StringComparison.Ordinal);

        var plain = Analyze(synchronous, "Wiring.cs");
        Assert.Single(plain, static d => d.Id == RuleIds.FireAndForgetTask);
        Assert.DoesNotContain(plain, static d => d.Id == RuleIds.AsyncVoidLambda);
    }

    /// <summary>
    ///     ⚠ <c>SK3051</c> and <c>SK3004</c> on one body, separated only by the token count.
    /// </summary>
    /// <remarks>
    ///     The call that wants a token is the same call in both halves, and the method is <c>async</c> in
    ///     both — so both rules' subject matter is present twice and only the count moves. This is also
    ///     the handover: adding the parameter is <c>SK3051</c>'s own fix, and what it produces is exactly
    ///     an <c>SK3004</c>.
    /// </remarks>
    [Fact]
    public void TheCancellationPair_HandsOverWhenTheTokenCountChanges() {
        const string source = """
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Loader {
                                  public async Task<string> LoadAsync(string path) {
                                      return await File.ReadAllTextAsync(path);
                                  }
                              }
                              """;

        var without = Analyze(source, "Loader.cs");
        Assert.Single(without, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
        Assert.DoesNotContain(without, static d => d.Id == RuleIds.CancellationTokenNotForwarded);

        // ⚠ `token`, not `cancellationToken`. The conventional name would hit SK3051's CS0100 guard
        // first, and the second half would then pass without the token count deciding anything.
        var with = source.Replace(
            "LoadAsync(string path)",
            "LoadAsync(string path, CancellationToken token)",
            StringComparison.Ordinal
        );

        var after = Analyze(with, "Loader.cs");
        Assert.Single(after, static d => d.Id == RuleIds.CancellationTokenNotForwarded);
        Assert.DoesNotContain(after, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
    }

    /// <summary>
    ///     ⚠ <c>SK3051</c>'s fix appends the parameter <em>and</em> supplies it, so nothing is left over.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>This test asserted the opposite until #328, and the assertion it made was the defect.</b>
    ///     It required an <c>SK3004</c> to survive the fix — which is exactly the state the issue
    ///     objected to: a parameter added to a signature and forwarded to nothing, with the rule that
    ///     would have said so silenced because it looks for the parameter. The finding SK3004 used to
    ///     make here is now made by the edit instead.
    /// </remarks>
    [Fact]
    public void TheCancellationFix_AppendsAnOptionalTokenAndForwardsIt() {
        const string source = """
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Loader {
                                  public async Task<string> LoadAsync(string path) {
                                      return await File.ReadAllTextAsync(path);
                                  }
                              }
                              """;

        var fixedText = Apply(source, RuleIds.AsyncMethodWithoutCancellation);

        Assert.Contains(
            "LoadAsync(string path, CancellationToken cancellationToken = default)",
            fixedText,
            StringComparison.Ordinal
        );

        Assert.Contains(
            "File.ReadAllTextAsync(path, cancellationToken: cancellationToken)",
            fixedText,
            StringComparison.Ordinal
        );

        var after = Analyze(fixedText, "Loader.cs");
        Assert.DoesNotContain(after, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
        Assert.DoesNotContain(after, static d => d.Id == RuleIds.CancellationTokenNotForwarded);
    }

    /// <summary>
    ///     ⚠ #328's own shape: two awaited calls that can take a token, and both must get it.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The regression this pins is a parameter forwarded to nothing.</b> Measured on
    ///     <c>Tools/Rikarin.Skala.Server/LanguageServer.cs</c>, the fix added
    ///     <c>CancellationToken cancellationToken = default</c> to <c>ReadMessageAsync</c> and left
    ///     <c>input.ReadLineAsync()</c> and <c>input.ReadAsync(…)</c> untouched, so the signature
    ///     advertised a cancellation the body dropped at every call site. Both shapes are here: an
    ///     appended overload (<c>ReadLineAsync</c>) and an omitted optional parameter
    ///     (<c>ReadToEndAsync</c> has neither, so <c>Task.Delay</c> stands in for the second).
    ///     <para>
    ///         ⚠ The <c>WriteAsync</c> call is deliberately one no token can reach, and the method is
    ///         still reported. A token is a promise about the awaits that can honour it.
    ///     </para>
    /// </remarks>
    [Fact]
    public void TheCancellationFix_ForwardsToEveryCallThatCanTakeOne() {
        const string source = """
                              using System.IO;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Channel {
                                  readonly TextReader input = TextReader.Null;
                                  readonly TextWriter output = TextWriter.Null;

                                  public async Task PumpAsync() {
                                      var line = await input.ReadLineAsync();
                                      await output.WriteAsync(line);
                                      await Task.Delay(1);
                                  }
                              }
                              """;

        var fixedText = Apply(source, RuleIds.AsyncMethodWithoutCancellation);

        Assert.Contains(
            "PumpAsync(CancellationToken cancellationToken = default)",
            fixedText,
            StringComparison.Ordinal
        );

        Assert.Contains("input.ReadLineAsync(cancellationToken)", fixedText, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(1, cancellationToken)", fixedText, StringComparison.Ordinal);

        // ⚠ `TextWriter.WriteAsync(string?)` has no token-taking sibling, so it keeps its argument
        // list — and its presence does not withdraw the finding.
        Assert.Contains("output.WriteAsync(line)", fixedText, StringComparison.Ordinal);

        var after = Analyze(fixedText, "Channel.cs");
        Assert.DoesNotContain(after, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
        Assert.DoesNotContain(after, static d => d.Id == RuleIds.CancellationTokenNotForwarded);
    }

    /// <summary>
    ///     ⚠ CS8421: a <c>static</c> lambda cannot capture the parameter, so the site is declined.
    /// </summary>
    /// <remarks>
    ///     The only call that would take a token is inside a <c>static</c> lambda. Forwarding there does
    ///     not compile and appending the parameter alone is the #328 defect, so the finding is
    ///     withdrawn rather than half-repaired.
    /// </remarks>
    [Fact]
    public void ACallInsideAStaticLambda_WithdrawsSk3051() {
        const string source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Scheduler {
                                  public async Task RunAsync() {
                                      Func<Task> work = static () => Task.Delay(10);
                                      await work();
                                  }
                              }
                              """;

        Assert.DoesNotContain(
            Analyze(source, "Scheduler.cs"),
            static d => d.Id == RuleIds.AsyncMethodWithoutCancellation
        );
    }

    /// <summary>
    ///     ⚠ The insertion point when the parameter list is empty is the closing paren, not a comma.
    /// </summary>
    [Fact]
    public void TheCancellationFix_HandlesAnEmptyParameterList() {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Poller {
                                  public async Task PollAsync() {
                                      await Task.Delay(50);
                                  }
                              }
                              """;

        Assert.Contains(
            "PollAsync(CancellationToken cancellationToken = default)",
            Apply(source, RuleIds.AsyncMethodWithoutCancellation),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     Every shape that makes one of the three walks run out of tree, asserting only nothing threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately <em>not</em> about what is reported. Each of these is a shape
    ///     the walk has to survive rather than one it has to judge, and pinning the verdicts here would
    ///     turn a robustness test into a second copy of the fixtures — the version of this test that
    ///     quietly stops testing anything. ⚠ It includes a call that does not bind, because a lambda
    ///     whose conversion fails has no converted type and error recovery is where a null slips through.
    /// </remarks>
    [Fact]
    public void ADegenerateAsyncShape_DoesNotCrashAnAnalyzer() {
        const string source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Degenerate {
                                  public async void ThrowsFromAFinally() {
                                      try {
                                          await Task.Yield();
                                      } finally {
                                          throw new InvalidOperationException();
                                      }
                                  }

                                  public async void ThrowsFromANestedCatch() {
                                      try {
                                          try {
                                              await Task.Yield();
                                          } catch (InvalidOperationException) {
                                              throw;
                                          }
                                      } catch (Exception) {
                                          throw;
                                      }
                                  }

                                  public async void Nested() {
                                      Action outer = async () => {
                                          Action inner = async () => await Task.Yield();
                                          inner();
                                          await Task.Yield();
                                      };

                                      outer();
                                      await Task.Yield();
                                  }

                                  public void Unbound() {
                                      NoSuchMethod(async () => await Task.Yield());
                                  }

                                  public async Task ManyParameters(int a, string b = "x", params int[] rest) {
                                      await Task.Delay(a);
                                  }

                                  public async Task LocalsAllTheWayDown() {
                                      static async void Inner() {
                                          await Task.Yield();
                                          throw new InvalidOperationException();
                                      }

                                      Inner();
                                      await Task.Delay(1);
                                  }

                                  public async Task<int> Expression() => await Task.FromResult(1);

                                  public async void Empty() { }
                              }
                              """;

        Assert.DoesNotContain(Analyze(source, "Degenerate.cs"), static d => d.Id == "AD0001");
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: an analyzer set that never runs also never crashes.
    /// </summary>
    /// <remarks>
    ///     A compilation where <c>CancellationToken</c> or <c>Task</c> does not resolve makes two of
    ///     these three return from <c>RegisterCompilationStartAction</c> without registering anything, so
    ///     "no <c>AD0001</c>" would be a fact about the binding and not about the walks. This asserts the
    ///     degenerate source really does reach all three.
    /// </remarks>
    [Fact]
    public void TheDegenerateShapes_ReallyReachAllThreeRules() {
        const string source = """
                              using System;
                              using System.Threading;
                              using System.Threading.Tasks;

                              public sealed class Reached {
                                  public async void Throws() {
                                      await Task.Yield();
                                      throw new InvalidOperationException();
                                  }

                                  public void Wire() {
                                      Action callback = async () => await Task.Yield();
                                      callback();
                                  }

                                  public async Task PollAsync() {
                                      await Task.Delay(50);
                                  }
                              }
                              """;

        var diagnostics = Analyze(source, "Reached.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == "AD0001");
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.AsyncVoidThrow);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.AsyncVoidLambda);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
    }

    /// <summary>
    ///     ⚠ The one guard on <c>SK3051</c> that no fixture can see, because it is about another file.
    /// </summary>
    /// <remarks>
    ///     Appending an optional parameter is CS0123 at a method group conversion, and the conversion may
    ///     be anywhere in the compilation. Two files, one declaring and one converting, is the shape the
    ///     compilation-wide name scan exists for — and the first half proves the rule fires on the same
    ///     declaration when nothing converts it.
    /// </remarks>
    [Fact]
    public void AMethodGroupConversionInAnotherFile_WithdrawsSk3051() {
        const string declaring = """
                                 using System.Threading.Tasks;

                                 public sealed class Poller {
                                     public async Task PollAsync() {
                                         await Task.Delay(50);
                                     }
                                 }
                                 """;

        const string converting = """
                                  using System;
                                  using System.Threading.Tasks;

                                  public sealed class Host {
                                      public Func<Task> Handler(Poller poller) => poller.PollAsync;
                                  }
                                  """;

        Assert.Single(Analyze(declaring, "Poller.cs"), static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);

        var second = CSharpSyntaxTree.ParseText(
            converting,
            new CSharpParseOptions(LanguageVersion.Preview),
            "Host.cs",
            cancellationToken: TestContext.Current.CancellationToken
        );

        var both = RuleFixtures.Analyze(
            RuleFixtures.Compile(declaring, "Poller.cs").AddSyntaxTrees(second),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(both, static d => d.Id == RuleIds.AsyncMethodWithoutCancellation);
    }

    static ImmutableArray<Diagnostic> Analyze(string source, string path) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, path),
            Analyzers,
            TestContext.Current.CancellationToken
        );

    static string Apply(string source, string ruleId) {
        var edits = Analyze(source, "Fixture.cs")
            .Where(diagnostic => diagnostic.Id == ruleId)
            .SelectMany(static diagnostic => {
                    var result = new List<(int Start, int Length, string Text)>();
                    var count = int.Parse(
                        diagnostic.Properties[FixEdits.CountKey]!,
                        System.Globalization.CultureInfo.InvariantCulture
                    );

                    for (var i = 0; i < count; i++) {
                        result.Add(
                            (
                                int.Parse(
                                    diagnostic.Properties[FixEdits.StartKey(i)]!,
                                    System.Globalization.CultureInfo.InvariantCulture
                                ),
                                int.Parse(
                                    diagnostic.Properties[FixEdits.LengthKey(i)]!,
                                    System.Globalization.CultureInfo.InvariantCulture
                                ),
                                diagnostic.Properties[FixEdits.TextKey(i)] ?? string.Empty
                            )
                        );
                    }

                    return result;
                }
            )
            .OrderByDescending(static edit => edit.Start)
            .ToArray();

        Assert.NotEmpty(edits);

        var text = source;
        foreach (var (start, length, replacement) in edits) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        return text;
    }
}
