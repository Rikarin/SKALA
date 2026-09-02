using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Async;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Globalization;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     The two rules about the shape of an <c>async</c> method rather than what it does.
/// </summary>
/// <remarks>
///     ⚠ <c>SK3007</c> is in the analyzer list on purpose. <c>SK3031</c>'s fix elides an <c>await</c>,
///     which is exactly the bug <c>SK3007</c> reports when it happens inside a <c>using</c> — so "the
///     rule stays quiet where its own fix would create an <c>SK3007</c>" is a property of the pair and
///     asserting it needs both running at once.
/// </remarks>
public sealed class AsyncShapeBatchTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new AsyncIteratorNotEnumeratedAnalyzer(), new AsyncOnlyToAwaitAnalyzer(),
        new TaskReturnedFromUsingAnalyzer()
    ];

    public static TheoryData<RuleFixture> Fixtures {
        get {
            var data = new TheoryData<RuleFixture>();
            foreach (var fixture in RuleFixtures.All().Where(static f => f.RuleId is "SK3030" or "SK3031")) {
                data.Add(fixture);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixtures_HaveExactCountsAndCarryTheirFix(RuleFixture fixture) {
        var findings = Analyze(RuleFixtures.Compile(File.ReadAllText(fixture.Path), fixture.Path))
            .Where(diagnostic => diagnostic.Id == fixture.RuleId)
            .ToArray();

        Assert.Equal(fixture.ShouldFire ? 1 : 0, findings.Length);

        var fixable = RuleCatalog.Get(fixture.RuleId).HasFix;
        Assert.All(findings, d => Assert.Equal(fixable, d.Properties.ContainsKey(FixEdits.CountKey)));
    }

    /// <summary>⚠ The whole statement becomes the loop, and the closing brace lands on the indent.</summary>
    [Fact]
    public void TheIteratorFix_WrapsTheCallInAnAwaitForeach() {
        var source = File.ReadAllText(
            Path.Combine(RuleFixtures.Root, "SK3030", "positive", "an-invocation-as-a-statement.cs")
        );

        Assert.Contains(
            "        await foreach (var _ in ProcessAsync()) {\n        }\n",
            Apply(source, "SK3030"),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     ⚠ The two guards that keep the fix compiling, each pinned by the fixture that breaks it.
    /// </summary>
    /// <remarks>
    ///     A synchronous enclosing body makes the inserted <c>await</c> CS4033, and a local already
    ///     named <c>_</c> makes the loop variable CS0136. Both are shapes where the bug is real and the
    ///     report is withheld, which is doc 08's rule about a finding an agent cannot act on.
    /// </remarks>
    [Theory]
    [InlineData("a-synchronous-method")]
    [InlineData("an-underscore-is-already-in-scope")]
    [InlineData("inside-a-lock")]
    public void WhereTheRewriteWouldNotCompile_NothingIsReported(string name) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK3030", "negative", name + ".cs"));

        Assert.DoesNotContain(Analyze(RuleFixtures.Compile(source, name + ".cs")), static d => d.Id == "SK3030");
    }

    /// <summary>⚠ Three body shapes, two of them one edit and the third two.</summary>
    [Theory]
    [InlineData("a-return-await-body", "    public Task<int> CountAsync() {\n        return LoadAsync();\n    }")]
    [InlineData("an-expression-bodied-method", "    public ValueTask<int> CountAsync() => LoadAsync();")]
    [InlineData("a-non-generic-task-body", "    public Task FlushAsync() {\n        return WriteAsync();\n    }")]
    public void TheElisionFix_RemovesTheStateMachineAndKeepsTheCall(string name, string expected) {
        var source = File.ReadAllText(Path.Combine(RuleFixtures.Root, "SK3031", "positive", name + ".cs"));

        Assert.Contains(expected, Apply(source, "SK3031"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     ⚠ <c>SK3031</c>'s edit, applied to the shape it refuses, is exactly <c>SK3007</c>'s bug.
    /// </summary>
    /// <remarks>
    ///     This is the pin the pair needs and it runs in both directions. The fixture is silent under
    ///     <c>SK3031</c> — its body is two statements, and no shape this rule matches can hold a
    ///     <c>using</c> — and the same file with <c>async</c> and <c>await</c> taken out by hand is
    ///     reported by <c>SK3007</c>, because the <c>using</c> now disposes the stream at the
    ///     <c>return</c>, before the task it produced has finished. Widening <c>SK3031</c> past a
    ///     single-statement body would make it recommend that edit, and the two rules would then
    ///     disagree about one line with only one of them right.
    ///     <para>
    ///         ⚠ It is also why <c>supersedes</c> is not used. <c>Supersession.Apply</c> suppresses the
    ///         <em>superseded</em> finding on a shared span, which here would hide <c>SK3007</c> — the
    ///         one that carries the remedy.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ElidingTheAwaitInsideAUsing_IsTheBugSk3007Reports() {
        var path = Path.Combine(RuleFixtures.Root, "SK3031", "negative", "the-await-is-inside-a-using.cs");
        var source = File.ReadAllText(path);

        var before = Analyze(RuleFixtures.Compile(source, path));
        Assert.DoesNotContain(before, static d => d.Id == "SK3031");
        Assert.DoesNotContain(before, static d => d.Id == "SK3007");

        var elided = source.Replace("async Task<int> ReadAsync", "Task<int> ReadAsync", StringComparison.Ordinal)
            .Replace("return await stream", "return stream", StringComparison.Ordinal);

        Assert.Single(Analyze(RuleFixtures.Compile(elided, path)), static d => d.Id == "SK3007");
    }

    [Fact]
    public void GeneratedCode_IsIgnored() {
        var source = "// <auto-generated/>\n"
            + File.ReadAllText(
                Path.Combine(RuleFixtures.Root, "SK3030", "positive", "an-invocation-as-a-statement.cs")
            );

        Assert.Empty(Analyze(RuleFixtures.Compile(source, "generated.cs")));
    }

    static ImmutableArray<Diagnostic> Analyze(CSharpCompilation compilation) =>
        RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);

    static string Apply(string source, string id) {
        var diagnostic = Assert.Single(Analyze(RuleFixtures.Compile(source, "probe.cs")).Where(d => d.Id == id));

        var count = int.Parse(diagnostic.Properties[FixEdits.CountKey]!, CultureInfo.InvariantCulture);
        var edits = Enumerable.Range(0, count)
            .Select(index => new TextChange(
                    new TextSpan(
                        int.Parse(diagnostic.Properties[FixEdits.StartKey(index)]!, CultureInfo.InvariantCulture),
                        int.Parse(diagnostic.Properties[FixEdits.LengthKey(index)]!, CultureInfo.InvariantCulture)
                    ),
                    diagnostic.Properties[FixEdits.TextKey(index)]!
                )
            );

        return SourceText.From(source).WithChanges(edits).ToString();
    }
}
