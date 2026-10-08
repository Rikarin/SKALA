using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     #422: no fix is safe where it changes text a <c>[CallerArgumentExpression]</c> parameter captures.
/// </summary>
public sealed class CallerArgumentFixSafetyTests {
    static readonly string ProbePath = Path.Combine(
        RuleFixtures.Root,
        "SK4020",
        "positive",
        "inside-caller-argument-expressions.cs"
    );

    /// <summary>
    ///     The probe, both ways: every catalogue-safe edit applied changes <c>Probe.Run()</c>, and only the
    ///     edits <see cref="FixEdits.IsSafe(Diagnostic, SemanticModel, CancellationToken)" /> admits leave
    ///     it alone.
    /// </summary>
    /// <remarks>
    ///     ⚠ The first half is the instrument check. A probe the unguarded fixes cannot move would pass
    ///     with the guard deleted, which is the vacuous green this test exists to rule out.
    /// </remarks>
    [Fact]
    public void EveryCaptureShape_DeclinesTheSafeMark_AndThePreservedFixesKeepTheCapturedText() {
        var cancellation = TestContext.Current.CancellationToken;
        var source = File.ReadAllText(ProbePath);
        var before = RuleFixtures.Compile(source, ProbePath);
        var model = before.GetSemanticModel(before.SyntaxTrees.Single());
        var expected = CrossFixtureFixTests.Probe(before, cancellation);
        Assert.NotNull(expected);

        var findings = RuleFixtures.Analyze(before, SkalaAnalyzers.All, cancellation)
            .Where(static diagnostic => RuleCatalog.Find(diagnostic.Id) is { HasFix: true, FixIsSafe: true })
            .ToArray();
        var declined = findings.Where(diagnostic => !FixEdits.IsSafe(diagnostic, model, cancellation)).ToArray();
        var admitted = findings.Except(declined).ToArray();

        // Thirteen capturing shapes in the probe, one SK4020 lambda in each.
        var declinedLines = declined.Where(static d => d.Id == "SK4020")
            .Select(static d => d.Location.GetLineSpan().StartLinePosition.Line + 1)
            .Distinct()
            .Count();
        Assert.True(
            declinedLines >= 13,
            $"Only {declinedLines} SK4020 finding line(s) were declined:\n  "
            + string.Join("\n  ", declined.Select(static d => d.Id + " " + d.Location.GetLineSpan()))
            + "\nall:\n  "
            + string.Join("\n  ", findings.Select(static d => d.Id + " " + d.Location.GetLineSpan()))
        );

        // The three lambdas outside any capture keep their safe mark: the explicit message, the
        // misnamed attribute and the plain call.
        var admittedLambdas = admitted.Count(static d => d.Id == "SK4020");
        Assert.True(
            admittedLambdas >= 3,
            $"Only {admittedLambdas} SK4020 finding(s) kept the safe mark; the guard over-declines:\n  "
            + string.Join("\n  ", admitted.Select(static d => d.Id + " " + d.Location.GetLineSpan()))
        );

        var unguarded = CrossFixtureFixTests.Probe(
            RuleFixtures.Compile(Apply(source, findings), ProbePath),
            cancellation
        );
        Assert.NotEqual(expected, unguarded);

        var guarded = CrossFixtureFixTests.Probe(
            RuleFixtures.Compile(Apply(source, admitted), ProbePath),
            cancellation
        );
        Assert.Equal(expected, guarded);
    }

    /// <summary>Spans the guard must and must not decline, named rather than inferred from a rule.</summary>
    [Theory]
    [InlineData("Capture(Apply(x => x + 1))", "x => x + 1", true)]
    [InlineData("Capture(value: Apply(x => x + 1))", "x => x + 1", true)]
    [InlineData("Capture(text: \"m\", value: Apply(x => x + 1))", "x => x + 1", false)]
    [InlineData("Capture(Apply(x => x + 1), \"m\")", "x => x + 1", false)]
    [InlineData("Misnamed(x => x + 1)", "x => x + 1", false)]
    [InlineData("Apply(x => x + 1).Text()", "x => x + 1", true)]
    [InlineData("Apply(x => x + 1)", "x => x + 1", false)]
    [InlineData("Many(values: x => x + 1)", "x => x + 1", true)]
    [InlineData("Many(values: x => x + 1)", "Many", true)]
    [InlineData("new Box(x => x + 1).Text", "x => x + 1", true)]
    [InlineData("Capture(Apply(x => x + 1))", "Capture(Apply(x => x + 1))", true)]
    [InlineData("Capture(Apply(x => x + 1))", "Capture", false)]
    [InlineData("Self(1)", "1", false)]
    [InlineData("Capture(Apply(x => x + 1))", "^x => x + 1", true)]
    [InlineData("Apply(x => x + 1)", "^x => x + 1", false)]
    [InlineData("Apply(x => x + 1).Said()", "x => x + 1", true)]
    [InlineData("Apply(x => x + 1).Said(\"m\")", "x => x + 1", false)]
    public void TheGuard_DeclinesExactlyTheCapturedText(string call, string edited, bool declines) {
        var source = $$"""
                       using System;
                       using System.Runtime.CompilerServices;

                       public sealed class Box {
                           public Box(Func<int, int> f, [CallerArgumentExpression("f")] string text = "") => Text = text;
                           public string Text { get; }
                       }

                       public static class Captures {
                           public static int Apply(Func<int, int> f) => f(1);
                           public static string Capture(object? value, [CallerArgumentExpression("value")] string text = "") => text;
                           public static string Many([CallerArgumentExpression("values")] string text = "", params Func<int, int>[] values) => text;
                           public static string Text<T>(this T value, [CallerArgumentExpression("value")] string text = "") => text;
                       #pragma warning disable CS8963, CS8965
                           public static string Misnamed(Func<int, int> f, [CallerArgumentExpression("nothing")] string text = "") => text;
                           public static string Self(int value, [CallerArgumentExpression("text")] string text = "") => text;
                       #pragma warning restore CS8963, CS8965
                           public static object Use() => {{call}};
                       }

                       public static class Blocks {
                           extension<T>(T value) {
                               public string Said([CallerArgumentExpression("value")] string text = "") => text;
                           }
                       }
                       """;
        var compilation = RuleFixtures.Compile(source, "planted.cs");
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static d => d.Severity == DiagnosticSeverity.Error)
        );

        var anchor = source.IndexOf("Use() => ", StringComparison.Ordinal) + "Use() => ".Length;
        // A leading `^` asks about an insertion at the text's start rather than a replacement of it.
        var insertion = edited.StartsWith('^');
        var text = insertion ? edited[1..] : edited;
        var start = source.IndexOf(text, anchor, StringComparison.Ordinal);
        var model = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());

        Assert.Equal(
            declines,
            CallerArgumentSafety.ChangesCapturedText(
                model,
                new(start, insertion ? 0 : text.Length),
                TestContext.Current.CancellationToken
            )
        );
    }

    /// <summary>
    ///     ⚠ The bypass check: the fix-edit property bag has one production reader, and it is the one
    ///     that sits beside the guard.
    /// </summary>
    /// <remarks>
    ///     <c>AnalyzerHost</c> is the only consumer that turns a diagnostic into an applicable fix, and it
    ///     does so through <see cref="FixEdits.Read" /> and
    ///     <see cref="FixEdits.IsSafe(string, IEnumerable{TextSpan}, SemanticModel, CancellationToken)" />.
    ///     A second reader of the keys anywhere in production code is a second path a fix can take out of
    ///     the analyzer without being asked about captured text — the shape #422 was, one rule at a time.
    /// </remarks>
    [Fact]
    public void NoProductionCode_ReadsFixEdits_ExceptThroughTheGuardedPath() {
        var root = Path.GetFullPath(Path.Combine(RuleFixtures.Root, "..", "..", ".."));
        var keyRead = new Regex(@"FixEdits\.(CountKey|StartKey|LengthKey|TextKey)\b", RegexOptions.CultureInvariant);
        var offenders = new List<string>();

        foreach (var directory in new[] {
                     "Analysis", "Core", "Distribution", "Formatting", "Reporting", "Rules", "Testing", "Tools"
                 }) {
            var path = Path.Combine(root, directory);
            if (!Directory.Exists(path)) {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)) {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains(".Tests/", StringComparison.Ordinal)
                    || relative.StartsWith("Testing/corpus/", StringComparison.Ordinal)
                    || relative == "Rules/Rikarin.Skala.Rules/SkalaRule.cs") {
                    continue;
                }

                if (keyRead.IsMatch(File.ReadAllText(file))) {
                    offenders.Add(relative);
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Production code reads the fix-edit keys outside FixEdits.Read, so its fixes skip the "
            + "[CallerArgumentExpression] guard:\n  "
            + string.Join("\n  ", offenders)
        );

        var host = File.ReadAllText(
            Path.Combine(root, "Analysis", "Rikarin.Skala.Analysis", "Hosting", "AnalyzerHost.cs")
        );
        Assert.Contains("FixEdits.Read(", host, StringComparison.Ordinal);
        Assert.Contains("FixEdits.IsSafe(", host, StringComparison.Ordinal);
    }

    static string Apply(string source, IEnumerable<Diagnostic> findings) {
        var text = source;
        var consumed = int.MaxValue;
        foreach (var (start, length, replacement) in findings.SelectMany(FixRoundTripTests.ReadEdits)
                     .OrderByDescending(static edit => edit.Start)) {
            if (start + length > consumed) {
                continue;
            }

            text = text[..start] + replacement + text[(start + length)..];
            consumed = start;
        }

        return text;
    }
}
