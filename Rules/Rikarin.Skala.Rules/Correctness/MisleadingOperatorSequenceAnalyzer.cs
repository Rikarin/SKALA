using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2063</c> — <c>x =- 1</c>, an <c>=</c> written hard against a unary operator so the pair
///     reads as <c>-=</c>.
/// </summary>
/// <remarks>
///     <c>x =- 1</c> assigns negative one; <c>x -= 1</c> subtracts one. C# has no <c>=-</c> operator,
///     so the compiler splits the two characters silently and the reader's eye does not.
///     <para>
///         ⚠ <b>Whitespace is the entire signal, and it is the only signal there is.</b> <c>x = -1</c>
///         and <c>x =- 1</c> are the same program; they differ in nothing a semantic model can see. So
///         this rule reads trivia, which no other correctness rule in the catalogue does, and it reads
///         the trivia of the tree <em>as parsed</em> — the source the author wrote. It draws no
///         conclusion from what the formatter would produce, and running the formatter is one of the
///         ways to make the finding go away.
///     </para>
///     <para>
///         ⚠ <b>Three conditions, and each one alone would over-fire.</b> There must be trivia before
///         the <c>=</c>; none between the <c>=</c> and the unary operator; and some between the unary
///         operator and its operand. That is the asymmetric spacing that groups the two operator
///         characters together and pushes the operand away. <c>x = -1</c>, <c>x =-1</c> and
///         <c>x=-1</c> all fail one of the three and are left alone — the middle one especially, which
///         is simply how somebody writing a negative literal in a hurry spells it.
///     </para>
///     <para>
///         ⚠ Only <c>-</c>, <c>+</c> and <c>!</c>: they are the unary operators whose character also
///         begins a compound assignment (<c>-=</c>, <c>+=</c>, <c>!=</c>). A <c>~</c> has no such
///         reading and is not examined.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MisleadingOperatorSequenceAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MisleadingOperatorSequence);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (assignment.Right is not PrefixUnaryExpressionSyntax unary
            || !unary.IsKind(SyntaxKind.UnaryMinusExpression)
            && !unary.IsKind(SyntaxKind.UnaryPlusExpression)
            && !unary.IsKind(SyntaxKind.LogicalNotExpression)) {
            return;
        }

        var equals = assignment.OperatorToken;
        var sign = unary.OperatorToken;

        // ⚠ Two conditions, and both are load-bearing. The `=` and the sign must be written as one
        // token, and the operand must be pushed away from the sign: that asymmetry is the whole
        // signal. `x = -1` and `x = - 1` fail the first; `x =-1` and `x=-1` fail the second, and
        // they are how a negative literal gets written in a hurry and in a compressed style.
        //
        // ⚠ A third condition — "there is space before the `=`" — was in the first draft and is
        // gone. A sabotage removing it turned nothing red, and the reason was that no fixture
        // separated it from the other two: `x=- 1` groups the operator characters together and
        // pushes the operand away exactly as `x =- 1` does, so declining it was an exemption with
        // no argument behind it.
        var closedUpToSign = !equals.HasTrailingTrivia && !sign.HasLeadingTrivia;
        var spacedFromOperand = sign.HasTrailingTrivia || unary.Operand.GetFirstToken().HasLeadingTrivia;

        if (!closedUpToSign || !spacedFromOperand) {
            return;
        }

        // ⚠ `-` and `+` read as the compound assignments `-=` and `+=`; `!` reads as the comparison
        // `!=`. Different mistakes, one shape, and the message names whichever one applies.
        var misread = sign.ValueText + "=";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(assignment.SyntaxTree, TextSpan.FromBounds(equals.SpanStart, sign.Span.End)),
                "`"
                + equals.ValueText
                + sign.ValueText
                + "` is not an operator and reads as `"
                + misread
                + "`; this assigns `"
                + sign.ValueText
                + unary.Operand.ToString().Trim()
                + "`"
            )
        );
    }
}
