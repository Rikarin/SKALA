using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2202</c> — a modification inside the part of a <c>?.</c> that runs only when the receiver
///     is not null.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2200 — events, delegates and effects that do not happen".
///     <para>
///         ⚠ <c>&amp;&amp;</c>, <c>||</c>, <c>??</c> and <c>?:</c> are deliberately not examined, and
///         declining them is the whole reason this rule is narrow enough to ship. Short-circuiting is
///         what those operators are <em>for</em>, so a side effect in their right operand is the idiom
///         rather than the defect. <c>?.</c> is different in kind: it states a check about the
///         <em>receiver</em>, and the arguments fall inside its reach as a consequence of precedence
///         rather than because anybody asked for it.
///     </para>
///     <para>
///         ⚠ It walks up from the modification rather than down from the conditional access, which is
///         what keeps the walk cheap and bounded — but it does <em>not</em> make <c>a?.Prop = 1</c> a
///         non-match on its own. C# 14's null-conditional assignment parses with the assignment
///         <em>as</em> the conditional part rather than as the parent of the conditional access, which
///         is the opposite of what the line reads like, and a fixture is what refuted the assumption.
///         The modification being the conditional part itself is therefore an explicit exclusion.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalInvocationSideEffectAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.ConditionalInvocationSideEffect);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.PreIncrementExpression,
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PostIncrementExpression,
            SyntaxKind.PostDecrementExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var modification = context.Node;

        for (var current = modification; current.Parent is not null; current = current.Parent) {
            // ⚠ A body whose run time is decided by somebody else is not decided by the `?.`. A
            // lambda or a local function inside the conditional part is invoked by whoever holds the
            // delegate, so the modification is not conditional in the sense the finding means — and
            // the walk stops rather than continuing to an enclosing conditional access.
            if (current.Parent is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) {
                return;
            }

            if (current.Parent is StatementSyntax or MemberDeclarationSyntax) {
                return;
            }

            if (current.Parent is ConditionalAccessExpressionSyntax access && access.WhenNotNull == current) {
                // ⚠ `box?.Value = 1` is C# 14's null-conditional assignment, and it parses with the
                // assignment *as* the conditional part rather than inside it — the opposite of what
                // the shape looks like on the page, and a false positive the fixture caught. There
                // the conditional write is the whole point of the line, so the modification being
                // the conditional part itself is the one arrangement that is never reported.
                if (current == modification) {
                    return;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        modification.GetLocation(),
                        "this runs only while `"
                        + RewriteGuards.Trim(access.Expression.ToString())
                        + "` is not null, so the modification is skipped whenever it is"
                    )
                );

                return;
            }
        }
    }
}
