using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Rikarin.Skala.Rules.Tests;

/// <summary>
///     ⚠ The instrument checks for #424's two proofs: each must refuse the edit that changes the program
///     and accept the one that does not, or every rule routed through it is passing for the reason a
///     disabled check passes.
/// </summary>
public sealed class FixProofTests {
    const string Path = "planted.cs";

    [Fact]
    public void TheReparse_RefusesADeletionThatTurnsTwoComparisonsIntoAGenericCall() {
        var (suppression, cancellation) = Suppression("F(a < b!, c > (d))");
        Assert.False(
            FixReparse.Preserves(
                suppression,
                suppression.Operand,
                cancellation,
                (suppression.OperatorToken.Span, string.Empty)
            )
        );
    }

    [Fact]
    public void TheReparse_AcceptsTheSameDeletionWhereNothingAroundItMoves() {
        var (suppression, cancellation) = Suppression("F(a < b!, c)");
        Assert.True(
            FixReparse.Preserves(
                suppression,
                suppression.Operand,
                cancellation,
                (suppression.OperatorToken.Span, string.Empty)
            )
        );
    }

    [Fact]
    public void TheReparse_RefusesTokensThatGlue() {
        var cancellation = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(
            "class C { int M(int a, int b) => a-(int)-b; }",
            cancellationToken: cancellation
        );
        var cast = tree.GetRoot(cancellation).DescendantNodes().OfType<CastExpressionSyntax>().Single();
        var edit = (TextSpan.FromBounds(cast.OpenParenToken.SpanStart, cast.Expression.SpanStart), string.Empty);

        Assert.False(FixReparse.Preserves(cast, cast.Expression, cancellation, edit));
        Assert.Null(FixReparse.Reparsed(cast, [edit], cancellation));
    }

    [Fact]
    public void TheReparse_FollowsAKindChangeAroundTheEdit() {
        var cancellation = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(
            "class C { bool M(object o) => o is string { }; }",
            cancellationToken: cancellation
        );
        var pattern = tree.GetRoot(cancellation).DescendantNodes().OfType<RecursivePatternSyntax>().Single();
        var edit = (TextSpan.FromBounds(pattern.Type!.Span.End, pattern.Span.End), string.Empty);

        // `o is string { }` is an is-pattern expression and `o is string` the `is` operator.
        Assert.IsType<BinaryExpressionSyntax>(FixReparse.Reparsed(pattern, [edit], cancellation));
    }

    [Fact]
    public void TheEquivalence_SeesAMovedSwitchSubjectAndIgnoresTrivia() {
        var cancellation = TestContext.Current.CancellationToken;
        var one = SyntaxFactory.ParseExpression("!(a == b) switch { true => a, false => b }");
        var two = SyntaxFactory.ParseExpression("a != b switch { true => a, false => b }");
        var spaced = SyntaxFactory.ParseExpression("!( a==b )  switch{true=>a,false=>b}");
        cancellation.ThrowIfCancellationRequested();

        Assert.False(FixReparse.Equivalent(one, two));
        Assert.True(FixReparse.Equivalent(one, spaced));
    }

    [Fact]
    public void TheRebind_SeesAMethodCalledNameof() {
        var cancellation = TestContext.Current.CancellationToken;
        const string source = """
                              public static class C {
                                  static string nameof(object o) => "x";

                                  public static string M(int count) => "cont";
                              }
                              """;
        var withMethod = Speculated(source, cancellation);
        var without = Speculated(
            source.Replace("static string nameof(object o) => \"x\";", string.Empty),
            cancellation
        );

        Assert.False(withMethod.HasValue);
        Assert.Equal("count", without.Value);
    }

    [Fact]
    public void TheIdentifier_EscapesAReservedKeywordAndNothingElse() {
        Assert.Equal("@class", FixRebind.Identifier("class"));
        Assert.Equal("value", FixRebind.Identifier("value"));
        Assert.Equal("count", FixRebind.Identifier("count"));
    }

    static Optional<object?> Speculated(string source, CancellationToken cancellation) {
        var compilation = RuleFixtures.Compile(source, Path);
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var literal = tree.GetRoot(cancellation)
            .DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .Single(static node => node.Token.ValueText == "cont");

        Assert.True(
            FixRebind.TrySpeculate(
                model,
                literal,
                SyntaxFactory.ParseExpression("nameof(count)"),
                out var speculative,
                out var placed
            )
        );
        return speculative.GetConstantValue(placed, cancellation);
    }

    static (PostfixUnaryExpressionSyntax Suppression, CancellationToken Cancellation) Suppression(string call) {
        var cancellation = TestContext.Current.CancellationToken;
        var tree = CSharpSyntaxTree.ParseText(
            "class C { void M(int a, int b, int c, int d) { " + call + "; } void F(bool x, bool y) { } }",
            cancellationToken: cancellation
        );
        return (
            tree.GetRoot(cancellation).DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().Single(),
            cancellation
        );
    }
}
