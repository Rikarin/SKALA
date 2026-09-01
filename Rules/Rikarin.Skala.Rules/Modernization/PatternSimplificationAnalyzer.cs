using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1051</c> — a <c>not</c> chain that cancels itself, and a negated relational pattern.
/// </summary>
/// <remarks>
///     ⚠ <b>This rule exists because of its neighbours.</b> <c>SK1011</c> and <c>SK1014</c>
///     <em>introduce</em> patterns and nothing tidies them afterwards, which is how a modernization
///     rule creates its own debt: patterns compose, so they also accumulate.
///     <para>
///         ⚠ <b><c>not (&gt; 5)</c> is <c>&lt;= 5</c> only where the order is total, and that is a
///         semantic question, so this rule is <c>Semantic</c> and not the <c>Syntax</c> its proposal
///         assumed.</b> On <c>double</c>, <c>NaN &gt; 5</c> is false, so <c>not (&gt; 5)</c> matches
///         <c>NaN</c> and <c>&lt;= 5</c> does not. The same trap with a different shape on
///         <c>int?</c>: <c>not (&gt; 5)</c> matches <c>null</c> and <c>&lt;= 5</c> does not. Both
///         rewrites look like De Morgan and neither is, so the inversion is admitted only for a
///         non-nullable type whose comparison is total — integral, <c>char</c>, <c>decimal</c>,
///         <c>nint</c> and enums.
///     </para>
///     <para>
///         ⚠ <b>The top of a <c>not</c> run owns the finding.</b> Reporting each adjacent pair would
///         produce overlapping fixes on a triple negation, and a rule that still fires after its own
///         fix turns <c>skala fix</c> into a loop. So the whole run collapses at once, and an odd
///         residual is folded into the relational inversion rather than left for a second pass.
///     </para>
///     <para>
///         ⚠ <b>The residual pattern keeps whatever parentheses it had.</b> <c>not</c> binds tighter
///         than <c>and</c>, so unwrapping the parentheses of <c>Z and not not (A or B)</c> would
///         produce <c>Z and A or B</c> — a different predicate.
///     </para>
///     <para>
///         ⚠ Not covered: <c>RedundantIsBeforeRelationalPattern</c>,
///         <c>ReplaceObjectPatternWithVarPattern</c>, <c>ExtractCommonPropertyPattern</c> and
///         <c>ReplaceSequenceEqualWithConstantPattern</c>. Each is a different rewrite with its own
///         proof obligation and none of them is this one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PatternSimplificationAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.SimplifiedPattern);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SimplifiedPattern);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.NotPattern);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var pattern = (UnaryPatternSyntax)context.Node;

        // The top of the run owns the rewrite; anything under another `not` is part of it.
        if (IsUnderNot(pattern)) {
            return;
        }

        var negations = 0;
        PatternSyntax residual = pattern;
        while (Unwrap(residual) is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negation) {
            negations++;
            residual = negation.Pattern;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var inverted = Unwrap(residual) is RelationalPatternSyntax relational
            && HasTotalOrder(InputType(model, pattern, cancellation))
                ? Invert(relational)
                : null;

        // One `not` is only a finding when the thing under it inverts; two or more always are.
        string replacement;
        if (negations % 2 == 0) {
            if (negations < 2) {
                return;
            }

            replacement = residual.ToString();
        } else if (inverted is not null) {
            replacement = inverted;
        } else if (negations >= 3) {
            replacement = "not " + residual;
        } else {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(pattern)
            || RewriteGuards.ContainsCommentOrDirective(pattern.SyntaxTree, pattern.FullSpan)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                pattern.GetLocation(),
                FixEdits.Pack((pattern.Span, replacement)),
                "The pattern simplifies to `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    static string Invert(RelationalPatternSyntax relational) {
        var operatorText = relational.OperatorToken.Kind() switch {
            SyntaxKind.GreaterThanToken => "<=",
            SyntaxKind.GreaterThanEqualsToken => "<",
            SyntaxKind.LessThanToken => ">=",
            _ => ">"
        };

        return operatorText + " " + relational.Expression;
    }

    /// <summary>
    ///     ⚠ Whether <c>not (&gt; c)</c> and <c>&lt;= c</c> are the same set of values.
    /// </summary>
    /// <remarks>
    ///     They are, exactly when every value of the type is either less than, equal to or greater than
    ///     <c>c</c>. A floating-point <c>NaN</c> is none of the three and a nullable type's <c>null</c>
    ///     is none of the three, and in both cases the value falls into the negated pattern and out of
    ///     the inverted one.
    /// </remarks>
    static bool HasTotalOrder(ITypeSymbol? type) {
        if (type is null || type.TypeKind == TypeKind.Error) {
            return false;
        }

        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) {
            return false;
        }

        if (type.TypeKind == TypeKind.Enum) {
            return true;
        }

        return type.SpecialType is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Char
            or SpecialType.System_Decimal
            or SpecialType.System_IntPtr
            or SpecialType.System_UIntPtr;
    }

    /// <summary>
    ///     The type the pattern is matched against, or null when it cannot be established here.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only a chain of <c>not</c>, <c>and</c>, <c>or</c> and parentheses preserves the input: a
    ///     property subpattern or a list pattern changes it to something this walk cannot see, and
    ///     guessing there would be guessing about which member is being tested.
    /// </remarks>
    static ITypeSymbol? InputType(SemanticModel model, SyntaxNode pattern, CancellationToken cancellation) {
        var current = pattern;
        while (current.Parent is UnaryPatternSyntax or BinaryPatternSyntax or ParenthesizedPatternSyntax) {
            current = current.Parent;
        }

        return current.Parent switch {
            IsPatternExpressionSyntax test => model.GetTypeInfo(test.Expression, cancellation).Type,
            SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax expression } =>
                model.GetTypeInfo(expression.GoverningExpression, cancellation).Type,
            CasePatternSwitchLabelSyntax {
                Parent: SwitchSectionSyntax { Parent: SwitchStatementSyntax statement }
            } => model.GetTypeInfo(statement.Expression, cancellation).Type,
            _ => null
        };
    }

    /// <summary>Whether this <c>not</c> is itself the operand of another one, parentheses aside.</summary>
    static bool IsUnderNot(SyntaxNode node) {
        var parent = node.Parent;
        while (parent is ParenthesizedPatternSyntax) {
            parent = parent.Parent;
        }

        return parent is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern };
    }

    static SyntaxNode Unwrap(SyntaxNode node) {
        while (node is ParenthesizedPatternSyntax parenthesized) {
            node = parenthesized.Pattern;
        }

        return node;
    }
}
