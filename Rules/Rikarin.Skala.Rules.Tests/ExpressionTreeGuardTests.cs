using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     What the expression-tree guard is for, where it is not needed, and the one type it misread.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         <c>SK1130</c>'s rewrite really is illegal inside an expression tree, and the rule really
///         has no guard against it.
///     </b> <c>span is "abc"</c> is a <em>constant pattern</em> — not the type-test operator that
///     <c>SK1120</c> emits — and a constant pattern in a lambda converted to
///     <c>Expression&lt;TDelegate&gt;</c> is CS8122. What makes the hole unreachable is a property of a
///     <em>different</em> language feature: the receiver has to be <c>Span&lt;char&gt;</c> or
///     <c>ReadOnlySpan&lt;char&gt;</c>, and a <c>ref struct</c> cannot appear in an expression tree at
///     all.
///     <para>
///         ⚠
///         <b>
///             That was verified against csc rather than reasoned about, and the verification changed
///             the story: the error is <c>CS8640</c>, not <c>CS8122</c>.
///         </b> "Expression tree cannot contain value of ref struct or restricted type" fires on the
///         <em>unrewritten</em> source, so the rule never sees the shape — every one of the four routes
///         a span could take into a tree is rejected before <c>SK1130</c> is asked anything: as the
///         lambda's own parameter, as the result of a call inside the tree, inside a delegate lambda
///         nested in the tree, and through the static <c>MemoryExtensions</c> spelling.
///     </para>
///     <para>
///         ⚠ <b>Which is why the guard is <em>not</em> added.</b> A check no fixture can turn red is a
///         zero from a disabled check, and this analyzer has already had one such check removed as dead
///         — the element-type test its own comment now describes. These tests are what #350 asked for
///         instead: the dependency is on <c>CS8640</c> continuing to hold, so <c>CS8640</c> is what is
///         asserted. If a future C# lifts it — and lifting the ref-struct restriction has been proposed
///         more than once — <see cref="ASpanReceiver_CannotReachAnExpressionTree" /> goes red and the
///         guard becomes required, which is the moment the issue was filed about.
///     </para>
/// </remarks>
public sealed class ExpressionTreeGuardTests {
    static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = [
        new ConstantPatternOverSequenceEqualAnalyzer(), new NullPatternAnalyzer()
    ];

    /// <summary>
    ///     ⚠ The predicate's own bug, and the one shape that makes it reachable (#349).
    /// </summary>
    /// <remarks>
    ///     <c>NullComparison.InsideExpressionTree</c> and <c>AsyncContext.InsideExpressionTree</c> both
    ///     asked <c>type.ToDisplayString().StartsWith("System.Linq.Expressions.Expression")</c>, which
    ///     answers <c>true</c> for anything in that namespace whose name merely <em>begins</em> with
    ///     <c>Expression</c>.
    ///     <para>
    ///         ⚠ <b>Reachability is the whole question, and it took a delegate to settle it.</b> A lambda
    ///         can only be converted to a delegate type or to an expression-tree type, so a user class
    ///         named <c>ExpressionFoo</c> can never be a lambda's <c>ConvertedType</c> and the bug would
    ///         have been unreachable. A user <em>delegate</em> declared in <c>System.Linq.Expressions</c>
    ///         can, it is perfectly legal, and every one of the ten-plus rules holding this guard went
    ///         silent inside a lambda converted to it — a false negative that looked exactly like clean
    ///         code. Fixed by comparing the namespace for equality, as <c>Formatting.CSharp</c>'s
    ///         <c>ExpressionTreeContext</c> already did.
    ///     </para>
    /// </remarks>
    [Fact]
    public void ADelegateWhoseNameMerelyStartsWithExpression_IsNotAnExpressionTree() {
        const string Source = """
                              using System;

                              namespace System.Linq.Expressions {
                                  public delegate bool ExpressionFilter(Row row);
                              }

                              public sealed class Row {
                                  public string? Name { get; set; }
                              }

                              public static class Probe {
                                  public static System.Linq.Expressions.ExpressionFilter Named() => row => row.Name != null;
                              }
                              """;

        var compilation = RuleFixtures.Compile(Source, "probe.cs");
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var findings = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.Contains(findings, static diagnostic => diagnostic.Id == RuleIds.IsNullPattern);
    }

    /// <summary>
    ///     ⚠ Anti-vacuity for the test above: the guard still has to fire on the real thing. Without
    ///     this, deleting the predicate outright would pass.
    /// </summary>
    [Fact]
    public void ARealExpressionTree_StillWithholdsThePatternFinding() {
        const string Source = """
                              using System;
                              using System.Linq.Expressions;

                              public sealed class Row {
                                  public string? Name { get; set; }
                              }

                              public static class Probe {
                                  public static Expression<Func<Row, bool>> Named() => row => row.Name != null;
                              }
                              """;

        var compilation = RuleFixtures.Compile(Source, "probe.cs");
        Assert.Empty(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken)
                .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
        );

        var findings = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(findings, static diagnostic => diagnostic.Id == RuleIds.IsNullPattern);
    }

    /// <summary>
    ///     Every route a <c>Span</c> receiver could take into an expression tree, and the error each draws.
    /// </summary>
    [Theory]
    [InlineData("s => s.SequenceEqual(\"abc\")", "Expression<Func<ReadOnlySpan<char>, bool>>")]
    [InlineData("s => s.AsSpan().SequenceEqual(\"abc\")", "Expression<Func<string, bool>>")]
    [InlineData("s => Apply(s, t => t.AsSpan().SequenceEqual(\"abc\"))", "Expression<Func<string, bool>>")]
    [InlineData("s => MemoryExtensions.SequenceEqual(s.AsSpan(), \"abc\")", "Expression<Func<string, bool>>")]
    public void ASpanReceiver_CannotReachAnExpressionTree(string lambda, string treeType) {
        var source = $$"""
                       using System;
                       using System.Linq.Expressions;

                       public static class Probe {
                           public static {{treeType}} Tree() => {{lambda}};

                           public static bool Apply(string value, Func<string, bool> predicate) => predicate(value);
                       }
                       """;

        var errors = RuleFixtures.Compile(source, "probe.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Contains(errors, static diagnostic => diagnostic.Id == "CS8640");
    }

    /// <summary>
    ///     ⚠ Anti-vacuity. The test above passes if the snippet is broken for any reason at all, so the
    ///     same call outside a tree has to compile clean <em>and</em> be a finding — otherwise "a span
    ///     cannot reach an expression tree" would be indistinguishable from "the probe does not compile".
    /// </summary>
    [Fact]
    public void TheSameCallOutsideATree_CompilesAndIsAFinding() {
        const string Source = """
                              using System;

                              public static class Probe {
                                  public static bool IsAbc(ReadOnlySpan<char> s) => s.SequenceEqual("abc");
                              }
                              """;

        var compilation = RuleFixtures.Compile(Source, "probe.cs");
        var errors = compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(errors);

        var findings = RuleFixtures.Analyze(compilation, Analyzers, TestContext.Current.CancellationToken);
        Assert.Contains(findings, static diagnostic => diagnostic.Id == RuleIds.ConstantPatternOverSequenceEqual);
    }

    /// <summary>
    ///     ⚠ The other half of the dependency: the rewrite <c>SK1130</c> would emit really is a pattern,
    ///     so if the receiver constraint ever widens the guard is genuinely needed. Asserted on
    ///     <c>string</c>, where the same constant pattern is legal outside a tree and CS8122 inside one.
    /// </summary>
    [Fact]
    public void TheRewriteIsAPattern_AndAPatternIsIllegalInATree() {
        const string Source = """
                              using System;
                              using System.Linq.Expressions;

                              public static class Probe {
                                  public static Expression<Func<string, bool>> Tree() => s => s is "abc";
                              }
                              """;

        var errors = RuleFixtures.Compile(Source, "probe.cs")
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Contains(errors, static diagnostic => diagnostic.Id == "CS8122");
    }
}
