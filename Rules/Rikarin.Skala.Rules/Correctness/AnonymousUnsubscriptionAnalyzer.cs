using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2201</c> — <c>changed -= (s, e) => …</c>, which removes nothing.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2200 — events, delegates and effects that do not happen".
///     <para>
///         ⚠ There is no heuristic here and no lifetime analysis. Delegate removal compares invocation
///         list entries by target and method, and an anonymous function written at one syntax site is a
///         different instance from one written at any other — including one that reads identically two
///         lines above. The removal is a no-op in every program, which is what makes the rule sound
///         where the general "this subscription is never undone" question is not decidable at all.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AnonymousUnsubscriptionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AnonymousUnsubscription);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.SubtractExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        ExpressionSyntax? left = null;
        ExpressionSyntax? right = null;
        switch (context.Node) {
            case AssignmentExpressionSyntax assignment:
                left = assignment.Left;
                right = assignment.Right;
                break;

            case BinaryExpressionSyntax binary:
                left = binary.Left;
                right = binary.Right;
                break;
        }

        if (left is null || right is null || Unwrap(right) is not AnonymousFunctionExpressionSyntax) {
            return;
        }

        // ⚠ The type of the left operand is what makes this semantic, and it is belt and braces
        // rather than the rule: an anonymous function cannot appear on the right of a numeric `-=`
        // and still compile. Reading it anyway means the rule declines rather than guesses when the
        // operand does not bind — in a file with errors, in a half-typed edit.
        var type = context.SemanticModel.GetTypeInfo(left, context.CancellationToken).Type;
        if (type is not { TypeKind: TypeKind.Delegate }) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "this removes nothing from `"
                + RewriteGuards.Trim(left.ToString())
                + "`; the delegate created here was never added to it"
            )
        );
    }

    /// <summary>A cast or parentheses change the spelling of an anonymous function, not its identity.</summary>
    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;

                default:
                    return expression;
            }
        }
    }
}
