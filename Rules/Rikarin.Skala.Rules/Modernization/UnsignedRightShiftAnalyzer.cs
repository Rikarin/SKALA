using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1064</c> — a cast down, a shift, and a cast back is the unsigned right shift.
/// </summary>
/// <remarks>
///     ⚠ <b>The equivalence has to be proved, not pattern-matched, and it fails just below 32 bits.</b>
///     <c>(short)((ushort)x >> n)</c> looks exactly like the <c>int</c> case and is a different
///     program: <c>ushort</c> promotes to <c>int</c> before <c>>></c> runs, so the value shifted is
///     zero-extended, while <c>x >>> n</c> on a <c>short</c> promotes the <em>signed</em> value first.
///     For <c>x = -1, n = 4</c> the first gives 4095 and the second gives -1. Only <c>int</c>/<c>uint</c>
///     and <c>long</c>/<c>ulong</c> — the widths at which <c>>></c> is actually defined — are admitted.
///     <para>
///         ⚠ <b>A <c>checked</c> context breaks it in both directions.</b> <c>(uint)x</c> throws for a
///         negative <c>x</c> and <c>(int)u</c> throws for a <c>u</c> above <c>int.MaxValue</c>, where
///         <c>>>></c> never throws at all.
///     </para>
///     <para>
///         ⚠ <b>And <c>>>></c> binds far more loosely than a cast.</b> <c>(int)((uint)x >> n) + 1</c>
///         rewritten bare is <c>x >>> (n + 1)</c>, so the rule declines every position where the
///         surrounding expression could rebind rather than inventing parentheses nobody wrote.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsignedRightShiftAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.UnsignedRightShift);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnsignedRightShift);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                // ⚠ #425: the compilation-wide `checked` switch was a gate here. It is a per-node
                // question now, answered by the bound conversion, so an `unchecked` region inside a
                // `CheckForOverflowUnderflow` project is reported and everything else in it is not.
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CastExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var outer = (CastExpressionSyntax)context.Node;
        if (Unwrap(outer.Expression) is not BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.RightShiftExpression
            } shift
            || Unwrap(shift.Left) is not CastExpressionSyntax inner) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var signed = model.GetTypeInfo(outer.Type, cancellation).Type?.SpecialType ?? SpecialType.None;
        var unsigned = model.GetTypeInfo(inner.Type, cancellation).Type?.SpecialType ?? SpecialType.None;

        // ⚠ The width test, and the whole of the proof. `>>` is defined for `int`, `uint`, `long` and
        // `ulong` and for nothing narrower — everything below promotes to `int` first, which is what
        // makes the `short`/`ushort` spelling of this shape a different program rather than a worse
        // one. The native integer types are refused for want of a stated width.
        if (!(signed == SpecialType.System_Int32 && unsigned == SpecialType.System_UInt32)
            && !(signed == SpecialType.System_Int64 && unsigned == SpecialType.System_UInt64)) {
            return;
        }

        // The operand's own type must be the signed one: `(int)(u >> n)` on an already-unsigned `u`
        // is a conversion of a result, not a round trip.
        if (model.GetTypeInfo(inner.Expression, cancellation).Type?.SpecialType != signed) {
            return;
        }

        if (model.GetTypeInfo(shift.Right, cancellation).Type?.SpecialType != SpecialType.System_Int32
            || IsChecked(model, outer, cancellation)
            || IsChecked(model, inner, cancellation)
            || !MayStandUnparenthesised(outer)
            || NullComparison.InsideExpressionTree(model, outer, cancellation)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(outer.SyntaxTree, outer.Span)) {
            return;
        }

        var replacement = inner.Expression + " >>> " + shift.Right;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                outer.GetLocation(),
                FixEdits.Pack((outer.Span, replacement)),
                "The cast round trip is an unsigned right shift: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>Whether the compiler emits this cast as a checked conversion, which can throw.</summary>
    /// <remarks>
    ///     ⚠ #425: this walked the syntax for the nearest <c>checked</c> and stopped at a lambda, but a
    ///     lambda written inside <c>checked { }</c> is checked too, and a project built with
    ///     <c>CheckForOverflowUnderflow</c> is checked everywhere no <c>unchecked</c> says otherwise. Measured for #412's
    ///     audit:
    ///     <c>OverflowException</c> before the fix and <c>2147483644</c> after it. The answer is read off
    ///     the conversion the compiler bound, which knows all three.
    /// </remarks>
    static bool IsChecked(
        SemanticModel model,
        CastExpressionSyntax cast,
        System.Threading.CancellationToken cancellation
    ) =>
        model.GetOperation(cast, cancellation) is not IConversionOperation { IsChecked: false };

    /// <summary>
    ///     ⚠ Whether a shift may be dropped into this position without parentheses.
    /// </summary>
    /// <remarks>
    ///     A cast binds at unary precedence and <c>>>></c> binds below every arithmetic operator, so
    ///     the replacement is looser than the text it replaces in every direction. <c>SK1050</c> asks
    ///     the same question about patterns for the same reason, and answers it the same way: name the
    ///     positions where the looser expression is safe and decline everything else, rather than
    ///     inventing parentheses the author did not write and the formatter is not allowed to remove.
    /// </remarks>
    static bool MayStandUnparenthesised(ExpressionSyntax expression) {
        var parent = expression.Parent;
        return parent switch {
            // `unchecked(...)` and `checked(...)` bracket their operand exactly as parentheses do.
            ParenthesizedExpressionSyntax or CheckedExpressionSyntax => true,
            ReturnStatementSyntax or ExpressionStatementSyntax or ArrowExpressionClauseSyntax => true,
            ArgumentSyntax or AttributeArgumentSyntax or EqualsValueClauseSyntax => true,
            InitializerExpressionSyntax => true,
            AssignmentExpressionSyntax assignment => assignment.Right == expression,
            ConditionalExpressionSyntax conditional => conditional.Condition != expression,
            _ => false
        };
    }

    static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
        while (expression is ParenthesizedExpressionSyntax parenthesized) {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}
