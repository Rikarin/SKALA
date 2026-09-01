using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Maintainability;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The batch level for <c>SK2070</c>–<c>SK2073</c> and <c>SK7110</c>: what the fixture harness
///     cannot ask.
/// </summary>
/// <remarks>
///     ⚠ <b>A crashed analyzer passes every negative fixture.</b> Roslyn catches the exception, reports
///     it as <c>AD0001</c>, and the analyzer produces nothing at all — so the positives fail, which
///     reads as "the rule is wrong", and every single "should not fire" fixture passes, which reads as
///     a clean false-positive record. The fixture harness does not check for it (issue #279), so these
///     tests do, over the inputs most likely to cause one: a template parser is a hand-written scanner
///     over attacker-shaped text, and its crash is an index it did not bound.
/// </remarks>
public sealed class LoggingBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new LogTemplateArgumentCountAnalyzer(), new LogTemplateDuplicatePropertyAnalyzer(),
        new InvisibleCharacterAnalyzer(), new CaughtExceptionNotLoggedAnalyzer(),
        new LoggerForAnotherTypeAnalyzer()
    ];

    /// <summary>
    ///     Every degenerate template shape, in one compilation, asserting only that nothing threw.
    /// </summary>
    /// <remarks>
    ///     ⚠ The assertion is deliberately <em>not</em> about what is reported. Each of these is a
    ///     template the parser has to survive rather than one it has to judge, and pinning the verdicts
    ///     here would turn a robustness test into a second copy of the fixtures that has to be updated
    ///     whenever a boundary moves — the version of this test that quietly stops testing anything.
    /// </remarks>
    [Fact]
    public void ADegenerateTemplate_DoesNotCrashAnAnalyzer() {
        const string source = """
                              namespace Serilog {
                                  interface ILogger {
                                      void Information(string messageTemplate, params object[] propertyValues);
                                      void Error(string messageTemplate, params object[] propertyValues);
                                      void Error(System.Exception exception, string messageTemplate, params object[] propertyValues);
                                  }
                              }

                              namespace Fixtures {
                                  sealed class Degenerate {
                                      public void Run(Serilog.ILogger logger, int value) {
                                          logger.Information("", value);
                                          logger.Information("{", value);
                                          logger.Information("}", value);
                                          logger.Information("{}", value);
                                          logger.Information("{@}", value);
                                          logger.Information("{$}", value);
                                          logger.Information("{,}", value);
                                          logger.Information("{:}", value);
                                          logger.Information("{,10}", value);
                                          logger.Information("{:N2}", value);
                                          logger.Information("{{", value);
                                          logger.Information("}}", value);
                                          logger.Information("{{}", value);
                                          logger.Information("{}}", value);
                                          logger.Information("{Unterminated", value);
                                          logger.Information("{Name", value);
                                          logger.Information("{@", value);
                                          logger.Information("{0", value);
                                          logger.Information("{ }", value);
                                          logger.Information("{Name}{", value);
                                          logger.Information("{Name,}", value);
                                          logger.Information("{Name:}", value);
                                          logger.Information("{{{Name}}}", value);
                                          logger.Information("{Name}{Name}{Name}", value);
                                      }
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
    /// <remarks>
    ///     A compilation where <c>Serilog.ILogger</c> does not resolve makes every one of these
    ///     analyzers return from <c>RegisterCompilationStartAction</c> without registering anything, so
    ///     "no <c>AD0001</c>" would be a fact about the binding and not about the parser. This asserts
    ///     that the same source really does reach the rules.
    /// </remarks>
    [Fact]
    public void TheDegenerateTemplates_ReallyReachTheRules() {
        const string source = """
                              namespace Serilog {
                                  interface ILogger {
                                      void Information(string messageTemplate, params object[] propertyValues);
                                  }
                              }

                              namespace Fixtures {
                                  sealed class Reached {
                                      public void Run(Serilog.ILogger logger, int value) {
                                          logger.Information("{Name} and {Other}", value);
                                          logger.Information("{Name} and {Name}", value, value);
                                      }
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Reached.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.LogTemplateArgumentCount);
        Assert.Contains(diagnostics, static d => d.Id == RuleIds.LogTemplateDuplicateProperty);
    }

    /// <summary>
    ///     ⚠ The scan runs over the token's source spelling, so a literal ending in a lone backslash or
    ///     an unterminated escape is text the scanner still has to walk past the end of.
    /// </summary>
    [Fact]
    public void AMalformedLiteral_DoesNotCrashTheInvisibleCharacterScan() {
        const string source = """
                              namespace Fixtures {
                                  sealed class Malformed {
                                      public const string Empty = "";
                                      public const string OneChar = "a";
                                      public const string Escapes = "\t\n\r\\\"A";
                                      public const char Char = 'a';
                                      public const string Interpolated = $"{Empty}{OneChar}";
                                      public const string Verbatim = @"";
                                  }
                              }
                              """;

        var diagnostics = RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "Malformed.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "AD0001");
    }
}
