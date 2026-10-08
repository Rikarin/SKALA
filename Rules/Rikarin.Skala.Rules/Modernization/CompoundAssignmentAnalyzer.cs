using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1041</c> — <c>x = x + 1</c> is <c>x += 1</c>.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         The two forms are not the same expression, and the difference runs the way that makes the
///         rewrite safe and the reverse unsafe.
///     </b>
///     C# defines <c>x op= y</c> as <c>x = (T)(x op y)</c>
///     with an <em>explicit</em> conversion back to the target's type, which the long form does not
///     have — so <c>byte b; b = b + 1;</c> does not compile at all while <c>b += 1;</c> does. Long form
///     to compound therefore never loses a conversion: the shapes where the difference matters are
///     shapes the compiler already rejected, and this rule never sees them.
///     <para>
///         ⚠ Which is exactly why a cast on the right-hand side is left alone. <c>l = (int)(l + 1)</c>
///         on a <c>long</c> truncates to 32 bits and widens back; <c>l += 1</c> does not, and they are
///         different programs. Unwrapping a cast would need a proof that it is precisely the narrowing
///         the compound form supplies, and the rule declines to attempt it rather than get it wrong for
///         one width.
///     </para>
///     <para>
///         ⚠ The other half is evaluation count, the same guard <c>SK1030</c> carries.
///         <c>a[i] = a[i] + 1</c> evaluates the indexer twice and <c>a[i] += 1</c> evaluates it once, so
///         the target is required to be a chain of plain names — and, since #423, one whose receiver
///         runs no getter, which is why the rule is semantic now.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CompoundAssignmentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CompoundAssignment);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // ⚠ An expression statement, so the assignment's own value is discarded. `f(x = x + 1)` has
        // the same value either way, but keeping the rewrite inside a statement keeps it obviously
        // local and costs nothing real. The same choice SK1030 made.
        if (assignment.Parent is not ExpressionStatementSyntax) {
            return;
        }

        if (assignment.Right is not BinaryExpressionSyntax binary || Operator(binary.Kind()) is not { } text) {
            return;
        }

        if (!RewriteGuards.IsPlainNamePath(assignment.Left)
            || !RewriteGuards.IsPlainNamePath(binary.Left)
            || !RewriteGuards.Same(assignment.Left, binary.Left)) {
            return;
        }

        // Everything between the target and the binary operator disappears. A comment in there is
        // content, and a directive is worse than content.
        var deleted = TextSpan.FromBounds(assignment.Left.Span.End, binary.OperatorToken.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(assignment.SyntaxTree, deleted)
            || !EvaluatesTheSame(context.SemanticModel, assignment, binary, text, context.CancellationToken)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(assignment.SyntaxTree, assignment.Span),
                FixEdits.Pack((deleted, " " + text + "=")),
                "Use a compound assignment: `"
                + RewriteGuards.Trim(assignment.Left + " " + text + "= " + binary.Right)
                + "`"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether <c>x op= y</c> runs exactly what <c>x = x op y</c> ran (#423).
    /// </summary>
    /// <remarks>
    ///     The long form evaluates the target's receiver twice — once as the place to store, once to
    ///     read — and the compound form once, so the receiver must be
    ///     <see cref="RewriteGuards.IsFreeToRepeat(IOperation)" />. The last link need not be: a
    ///     property's getter and setter each run once in both forms. #412's audit measured
    ///     <c>h.Current.X = h.Current.X + 5</c> over a counting getter as two reads before and one
    ///     after.
    ///     <para>
    ///         ⚠ And the operator must be the same method. C# 14 lets a type declare an instance
    ///         <c>operator +=</c>, which <c>a += 1</c> binds and <c>a = a + 1</c> never does: the audit
    ///         measured an alias of <c>a</c> left untouched before and mutated after. The compound form
    ///         is bound speculatively and must resolve to the binary operator's own method.
    ///     </para>
    /// </remarks>
    static bool EvaluatesTheSame(
        SemanticModel model,
        AssignmentExpressionSyntax assignment,
        BinaryExpressionSyntax binary,
        string text,
        CancellationToken cancellation
    ) {
        if (model.GetOperation(assignment, cancellation) is not ISimpleAssignmentOperation operation
            || model.GetOperation(binary, cancellation) is not IBinaryOperation arithmetic) {
            return false;
        }

        var receiver = operation.Target switch {
            ILocalReferenceOperation or IParameterReferenceOperation => null,
            IFieldReferenceOperation field => field.Instance,
            IPropertyReferenceOperation { Arguments.IsEmpty: true } property => property.Instance,
            _ => operation.Target
        };

        if (receiver is not null && !RewriteGuards.IsFreeToRepeat(receiver)) {
            return false;
        }

        if (arithmetic.OperatorMethod is null) {
            return true;
        }

        var compound = SyntaxFactory.ParseExpression(
            assignment.Left.WithoutTrivia() + " " + text + "= " + binary.Right.WithoutTrivia()
        );
        var bound = model.GetSpeculativeSymbolInfo(
            assignment.SpanStart,
            compound,
            SpeculativeBindingOption.BindAsExpression
        );
        return SymbolEqualityComparer.Default.Equals(bound.Symbol, arithmetic.OperatorMethod);
    }

    /// <summary>
    ///     The binary operators that have a compound form, and only those.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>&amp;&amp;</c> and <c>||</c> are absent because C# has no <c>&amp;&amp;=</c>: the
    ///     short-circuiting operators have no compound spelling, and rewriting <c>x = x &amp;&amp; y</c>
    ///     to <c>x &amp;= y</c> would evaluate <c>y</c> that the original skipped.
    /// </remarks>
    static string? Operator(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.AddExpression => "+",
            SyntaxKind.SubtractExpression => "-",
            SyntaxKind.MultiplyExpression => "*",
            SyntaxKind.DivideExpression => "/",
            SyntaxKind.ModuloExpression => "%",
            SyntaxKind.BitwiseAndExpression => "&",
            SyntaxKind.BitwiseOrExpression => "|",
            SyntaxKind.ExclusiveOrExpression => "^",
            SyntaxKind.LeftShiftExpression => "<<",
            SyntaxKind.RightShiftExpression => ">>",
            SyntaxKind.UnsignedRightShiftExpression => ">>>",
            _ => null
        };
}
