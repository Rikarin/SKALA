using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2121</c> — the <c>as</c> operator tests a conversion that always succeeds.
/// </summary>
/// <remarks>
///     <c>d as Base</c>, where <c>d</c> is a <c>Derived</c>, yields <c>d</c>. Always. The operator asks
///     a question the type system has already answered, and the <c>null</c> it appears to guard
///     against is only the operand's own <c>null</c> — so the check that usually follows reads as a
///     type guard and is a null guard.
///     <para>
///         ⚠
///         <b>
///             This is the half of issue #1 the compiler does not own, and probing established it is
///             the only half.
///         </b> Against the SDK at <c>AnalysisMode=All</c>, the always-<em>false</em>
///         cases are all compiler diagnostics: <c>s is int</c> and <c>d is Unrelated</c> are
///         <c>CS0184</c>, <c>d as Unrelated</c> is <c>CS0039</c>, an unreachable type pattern in a
///         <c>switch</c> is <c>CS8121</c>, and <c>v is int</c> on an <c>int</c> is <c>CS0183</c>. The
///         last two are errors. What survives is this operator, which is well defined and therefore
///         silent.
///     </para>
///     <para>
///         ⚠ <b>The always-true <c>is</c> check is deliberately not reported.</b> <c>d is Derived</c> is
///         <c>false</c> when <c>d</c> is null, so it is not always true — it is a null test, and
///         calling it redundant would require treating a nullable annotation as a runtime guarantee,
///         which <c>SK2001</c> already refuses to do. <c>as</c> needs no such assumption: it returns
///         the operand for every input including null.
///     </para>
///     <para>
///         ⚠ <b>Type parameters are excluded outright.</b> Inside a generic method the conversion is
///         classified against the constraint set rather than against the instantiated type, so a
///         conversion that looks certain there is not certain at run time.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AlwaysSucceedingAsAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AlwaysSucceedingAsOperator);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AsExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var expression = (BinaryExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (expression.ContainsDiagnostics
            || model.GetTypeInfo(expression.Right, cancellation).Type is not { } target
            || model.GetTypeInfo(expression.Left, cancellation).Type is not { } source
            || !IsUsable(target)
            || !IsUsable(source)) {
            return;
        }

        // ⚠ The two *types* are classified, never the expression. `SemanticModel.ClassifyConversion`
        // answers for an expression in a target context and would fold in conversions the `as` is not
        // performing; the operand's static type against the written type is exactly the question the
        // operator asks.
        if (model.Compilation is not CSharpCompilation compilation) {
            return;
        }

        var conversion = compilation.ClassifyConversion(source, target);
        if (!conversion.Exists
            || !conversion.IsIdentity
            && !(conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing))) {
            return;
        }

        // ⚠ The fix keeps the expression's static type. `var b = d as Base;` declares a `Base`, and
        // rewriting it to `d` would silently declare a `Derived` — the same trap the redundant-cast
        // rule documents for `var x = (long)1`. A widening therefore becomes the cast it always was,
        // written from the *syntax* on the right so the name stays the one that is in scope. Only an
        // identity conversion, where the type does not move, is replaced by the operand alone.
        var replacement = conversion.IsIdentity
            ? expression.Left.ToString()
            : Cast(expression.Right.ToString(), expression.Left);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                expression.OperatorToken.GetLocation(),
                FixEdits.Pack((expression.Span, replacement)),
                "`"
                + source.ToDisplayString()
                + "` always converts to `"
                + target.ToDisplayString()
                + "`, so this `as` returns its operand and can yield null only where the operand is null"
            )
        );
    }

    /// <summary>
    ///     ⚠ A cast binds tighter than almost every operator, so an operand that is not already a
    ///     primary expression keeps its own parentheses or <c>(Base)a ?? b</c> reassociates.
    /// </summary>
    static string Cast(string target, ExpressionSyntax operand) =>
        operand is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or CastExpressionSyntax
            or ThisExpressionSyntax
            or BaseExpressionSyntax
            or ObjectCreationExpressionSyntax
            ? "(" + target + ")" + operand
            : "(" + target + ")(" + operand + ")";

    /// <summary>
    ///     Types whose conversions are decided at compile time and stay decided at run time.
    /// </summary>
    static bool IsUsable(ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown);
}
