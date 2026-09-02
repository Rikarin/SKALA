using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2174</c> — an operand of a shift or bitwise operator that is itself an unparenthesised
///     binary expression from another precedence family.
/// </summary>
/// <remarks>
///     <c>a &lt;&lt; b + 1</c> shifts by <c>b + 1</c>. <c>mask &amp; offset + 1</c> masks against
///     <c>offset + 1</c>. <c>a | b &amp; c</c> is <c>a | (b &amp; c)</c>. Every one is what the grammar
///     says and none is what most readers say when asked, because the shift and bitwise operators sit
///     between arithmetic and comparison in a table nobody has memorised. There is no defect here to
///     find — only a sentence that can be written so it cannot be misread.
///     <para>
///         ⚠ <b>The boundary against <c>SK0209</c> is settled by construction, and settling it was the
///         condition on shipping this at all.</b> <c>skala arrange</c> removes redundant parentheses,
///         and <c>ParenthesesRedundancy.MayRemove</c> refuses unconditionally when the parent is a shift
///         or a bitwise operator, because <c>resharper_parentheses_non_obvious_operations</c> names
///         exactly those. Every pair of parentheses this rule adds has such a parent, so the arranger
///         will never take one back and <c>skala fix</c> and <c>skala arrange --aggressive</c> cannot
///         fight.
///     </para>
///     <para>
///         ⚠ <b>Disjoint from <c>SK2064</c> by construction.</b> A comparison operand under <c>&amp;</c>
///         or <c>|</c> only compiles when every operand is <c>bool</c>, which is <c>SK2064</c>'s
///         subject; it is declined here. Arithmetic and shift operands are never <c>bool</c>, so
///         <c>SK2064</c> fires only on <c>bool</c> operands and this rule only on integral ones, and no
///         expression can satisfy both.
///     </para>
///     <para>
///         ⚠ <b>The <c>?:</c> row this rule was drafted with is gone, because the grammar makes it
///         unreachable.</b> A conditional expression binds looser than every binary operator, so it can
///         never <em>be</em> an unparenthesised binary operand; the only reachable nesting is
///         <c>a ? b : c ? d : e</c>, the chained-ternary idiom every C# reader parses correctly.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnparenthesisedPrecedenceMixAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnparenthesisedPrecedenceMix);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.LeftShiftExpression,
            SyntaxKind.RightShiftExpression,
            SyntaxKind.UnsignedRightShiftExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.ExclusiveOrExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var parent = (BinaryExpressionSyntax)context.Node;
        Consider(context, parent, parent.Left);
        Consider(context, parent, parent.Right);
    }

    static void Consider(SyntaxNodeAnalysisContext context, BinaryExpressionSyntax parent, ExpressionSyntax operand) {
        if (operand is not BinaryExpressionSyntax inner
            || Family(inner.Kind()) is not { } innerFamily
            || Family(parent.Kind()) is not { } parentFamily
            || innerFamily == parentFamily) {
            return;
        }

        // ⚠ A shift under a bitwise operator is bit packing, and it is declined. `key << 8 |
        // digest[i]` and `value << offset & mask` are how every byte of every buffer has ever been
        // assembled, and no reader hesitates over them — the shift is visibly the thing being placed
        // and the bitwise operator is visibly the thing placing it. Measured rather than reasoned:
        // the rule without this line reports that shape once on Skala's own tree
        // (`CorpusSample.KeyOf`) and once in `pathological/operators-crammed-together.cs`, and both
        // are the idiom rather than the hazard. What is left is the pair the C precedence table
        // actually catches people out on: arithmetic bound looser than the shift or mask that
        // encloses it, and `&` bound tighter than `^` bound tighter than `|`.
        if (innerFamily == PrecedenceFamily.Shift && parentFamily != PrecedenceFamily.Shift) {
            return;
        }

        // ⚠ A `(` and a `)` inserted on either side of an `#if` do not necessarily both survive into
        // the same compilation.
        if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, inner.Span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                inner.GetLocation(),
                FixEdits.Pack((new TextSpan(inner.SpanStart, 0), "("), (new TextSpan(inner.Span.End, 0), ")")),
                "`"
                + inner.OperatorToken.ValueText
                + "` binds tighter than `"
                + parent.OperatorToken.ValueText
                + "`, so this operand is `"
                + RewriteGuards.Trim(inner.ToString().Trim())
                + "`; parenthesise it"
            )
        );
    }

    /// <summary>
    ///     The closed table. An operand is reported when its family differs from its parent's, and only
    ///     the families listed here are examined at all.
    /// </summary>
    /// <remarks>
    ///     ⚠ Comparison is absent, and <c>SK2064</c> is the reason: under <c>&amp;</c> or <c>|</c> a
    ///     comparison operand only compiles when every operand is <c>bool</c>, which that rule already
    ///     reports with the better advice. <c>&amp;&amp;</c>, <c>||</c> and <c>??</c> are absent because
    ///     they bind looser than every operator registered above and can never appear as one of their
    ///     unparenthesised operands.
    /// </remarks>
    static PrecedenceFamily? Family(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.MultiplyExpression
                or SyntaxKind.DivideExpression
                or SyntaxKind.ModuloExpression
                or SyntaxKind.AddExpression
                or SyntaxKind.SubtractExpression => PrecedenceFamily.Arithmetic,
            SyntaxKind.LeftShiftExpression
                or SyntaxKind.RightShiftExpression
                or SyntaxKind.UnsignedRightShiftExpression => PrecedenceFamily.Shift,
            SyntaxKind.BitwiseAndExpression => PrecedenceFamily.And,
            SyntaxKind.ExclusiveOrExpression => PrecedenceFamily.Xor,
            SyntaxKind.BitwiseOrExpression => PrecedenceFamily.Or,
            _ => null
        };

    enum PrecedenceFamily {
        Arithmetic,
        Shift,
        And,
        Xor,
        Or
    }
}
