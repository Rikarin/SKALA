using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Cleanup;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The <c>SK023x</c> cleanup family, one reported shape at a time.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         A rule that covers eight shapes and is tested for two is a rule with six untested
///         shapes.
///     </b> <see cref="RuleFixtureTests" /> asks only whether the rule fired on a file and
///     whether its fix parses and silences it; it never looks at what the fix produced. These rules
///     each retire several ReSharper inspections under one id, so the fix text is asserted here per
///     shape — which is what fails when a branch is deleted, and what would not fail if only the
///     fixture count were checked.
/// </remarks>
public sealed class CleanupRedundancyBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new EmptyInitializerAnalyzer(), new RedundantStringCallAnalyzer(),
    ];

    [Theory]
    // SK0230 — the empty `with`, which is the shape that makes the fix unsafe.
    [InlineData(
        "SK0230",
        "record P(int X); static class C { static P M(P p) => p with { }; }",
        "record P(int X); static class C { static P M(P p) => p; }"
    )]
    // SK0230 — an object initializer with no argument list has to grow one.
    [InlineData(
        "SK0230",
        "class O { public int D { get; set; } } static class C { static O M() => new O { }; }",
        "class O { public int D { get; set; } } static class C { static O M() => new O(); }"
    )]
    // SK0230 — an argument list that is already there is kept.
    [InlineData(
        "SK0230",
        "class O { public O(int d) { } public int D { get; set; } } static class C { static O M() => new O(1) { }; }",
        "class O { public O(int d) { } public int D { get; set; } } static class C { static O M() => new O(1); }"
    )]
    // SK0230 — a target-typed creation keeps its empty argument list.
    [InlineData(
        "SK0230",
        "class O { public int D { get; set; } } static class C { static O M() { O o = new() { }; return o; } }",
        "class O { public int D { get; set; } } static class C { static O M() { O o = new(); return o; } }"
    )]
    // SK0230 — a collection initializer collapses the same way an object initializer does.
    [InlineData(
        "SK0230",
        "using System.Collections.Generic; static class C { static List<int> M() => new List<int> { }; }",
        "using System.Collections.Generic; static class C { static List<int> M() => new List<int>(); }"
    )]
    // SK0231 — `ToString()` on something already a string.
    [InlineData(
        "SK0231",
        "static class C { static string M(string s) => s.ToString(); }",
        "static class C { static string M(string s) => s; }"
    )]
    // SK0231 — the `foreach` copy, which is the allocation rather than the noise.
    [InlineData(
        "SK0231",
        "static class C { static int M(string s) { var n = 0; foreach (var c in s.ToCharArray()) { n += c; } return n; } }",
        "static class C { static int M(string s) { var n = 0; foreach (var c in s) { n += c; } return n; } }"
    )]
    // SK0231 — `string.Format` of a literal with no placeholders is the literal.
    [InlineData(
        "SK0231",
        "static class C { static string M() => string.Format(\"plain\"); }",
        "static class C { static string M() => \"plain\"; }"
    )]
    // SK0231 — the `$` goes and nothing else does.
    [InlineData(
        "SK0231",
        "static class C { static string M() => $\"plain\"; }",
        "static class C { static string M() => \"plain\"; }"
    )]
    // SK0231 — and so does the `@`.
    [InlineData(
        "SK0231",
        "static class C { const string S = @\"plain\"; static string M() => S; }",
        "static class C { const string S = \"plain\"; static string M() => S; }"
    )]
    public void TheFix_ProducesExactlyThisText(string rule, string before, string after) =>
        Assert.Equal(after, Fix(rule, before));

    /// <summary>Applies every edit the rule carries, so the assertion is about the text, not the span.</summary>
    static string Fix(string rule, string source) {
        var findings = Analyze(source).Where(diagnostic => diagnostic.Id == rule).ToArray();
        Assert.True(findings.Length > 0, $"{rule} did not fire on:\n{source}");

        var edits = findings.SelectMany(Edits).OrderByDescending(static edit => edit.Start).ToArray();
        Assert.True(edits.Length > 0, $"{rule} fired and carried no fix on:\n{source}");

        var text = source;
        foreach (var (start, length, replacement) in edits) {
            text = text[..start] + replacement + text[(start + length)..];
        }

        return text;
    }

    static ImmutableArray<Diagnostic> Analyze(string source) =>
        RuleFixtures.Analyze(
            RuleFixtures.Compile(source, "batch.cs"),
            Analyzers,
            TestContext.Current.CancellationToken
        );

    static IEnumerable<(int Start, int Length, string Text)> Edits(Diagnostic diagnostic) {
        if (!diagnostic.Properties.TryGetValue(FixEdits.CountKey, out var countText)
            || !int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) {
            yield break;
        }

        for (var i = 0; i < count; i++) {
            yield return (
                int.Parse(diagnostic.Properties[FixEdits.StartKey(i)]!, CultureInfo.InvariantCulture),
                int.Parse(diagnostic.Properties[FixEdits.LengthKey(i)]!, CultureInfo.InvariantCulture),
                diagnostic.Properties[FixEdits.TextKey(i)] ?? string.Empty
            );
        }
    }
}
