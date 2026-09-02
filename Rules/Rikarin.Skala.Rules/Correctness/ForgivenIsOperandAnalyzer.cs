using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2172</c> — <c>x! is T</c>, a null-forgiving <c>!</c> standing where it reads as a negated
///     <c>is</c> and where it suppresses nothing.
/// </summary>
/// <remarks>
///     <c>x! is string</c> and <c>!(x is string)</c> differ by the position of one character and answer
///     opposite questions. The first is the one that compiles silently everywhere.
///     <para>
///         ⚠ <b>The <c>!</c> is inert, and that is measured rather than argued.</b> <c>is</c> never
///         issues a nullability warning about its own operand — a pattern match is exactly the test that
///         makes a null operand safe — so there is no warning there to suppress. A probe on SDK 10.0.400
///         compiled both spellings under <c>#nullable enable</c>: <c>if (s! is object) { }</c> followed
///         by <c>s.Length</c> reports <c>CS8602</c>, and so does the same code with the <c>!</c>
///         removed, at the same position.
///     </para>
///     <para>
///         ⚠ <b>That probe refuted the reason this rule was first going to carry a fix.</b> The
///         suppression was assumed to carry forward into the flow state, which would have made removing
///         it unsafe; it does not. The rule is fixless for the other reason instead — <c>x is not T</c>
///         and <c>x is T</c> are both plausible readings of <c>x! is T</c> and they are opposite
///         programs.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK2111</c> by construction, which is the only reason this rule is
///         <c>Semantic</c>.</b> <c>SK2111</c> owns the <c>!</c> that is inert because nullable warnings
///         are off at that position or because the operand is a non-nullable value type. Both are
///         declined here, so no <c>!</c> can be reported twice and neither rule needs to know about the
///         other's message.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForgivenIsOperandAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForgivenIsOperand);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IsPatternExpression, SyntaxKind.IsExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        // ⚠ Both node kinds, because the parser separates them and the reader cannot: `x! is T` is a
        // binary `IsExpression` and `x! is T t` is an `IsPatternExpression`, and the misreading is
        // identical in each.
        ExpressionSyntax operand;
        SyntaxToken keyword;
        if (context.Node is IsPatternExpressionSyntax pattern) {
            operand = pattern.Expression;
            keyword = pattern.IsKeyword;
        } else if (context.Node is BinaryExpressionSyntax binary) {
            operand = binary.Left;
            keyword = binary.OperatorToken;
        } else {
            return;
        }

        // ⚠ Only a `!` that is the *whole* left operand. `list![0] is string` and
        // `text!.Trim() is { Length: > 0 }` each suppress a real warning on something inside the
        // operand and are not this shape; a parenthesised `(x!) is string` is declined because the
        // parentheses say which operand the `!` binds to, which is the entire thing the reader of the
        // bare form cannot see.
        if (operand is not PostfixUnaryExpressionSyntax suppression
            || !suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression)) {
            return;
        }

        // ⚠ SK2111's two grounds, declined here so that the two rules are disjoint by construction
        // rather than by filter.
        if ((context.SemanticModel.GetNullableContext(suppression.SpanStart) & NullableContext.WarningsEnabled) == 0) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(suppression.Operand, context.CancellationToken).Type;
        if (type is null || type.IsValueType && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(
                    context.Node.SyntaxTree,
                    TextSpan.FromBounds(suppression.OperatorToken.SpanStart, keyword.Span.End)
                ),
                "`! is` reads as a negated `is`; the `!` suppresses nothing, because `is` issues no "
                + "nullability warning about its operand"
            )
        );
    }
}
