using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1120</c> — a reflection call that asks what the <c>is</c> operator answers.
/// </summary>
/// <remarks>
///     <c>typeof(T).IsInstanceOfType(x)</c> and <c>typeof(T).IsAssignableFrom(x.GetType())</c> both
///     load a <see cref="System.Type" /> and make a virtual call to answer a question one IL
///     instruction answers. The value is a modernization rather than a micro-optimisation: <c>is</c>
///     is the spelling a reader recognises, and it composes with a pattern — <c>x is T t</c> — where
///     the reflection call needs a second cast nobody checks.
///     <para>
///         ⚠ <b>The two shapes are one concept and they are <em>not</em> equally safe, which is why
///         the rule ships <c>fixIsSafe: false</c> although one half of it is exactly total.</b>
///         Fourteen shapes were compiled and run for <c>IsInstanceOfType</c> — null, a boxed value
///         type, a non-null and a null <c>int?</c>, an interface, a covariant interface, array
///         covariance, an array through <c>IList&lt;T&gt;</c>, an enum boxed as itself and as its
///         underlying <c>int</c> — and the reflection call and the operator agree on every one.
///         <c>IsAssignableFrom(x.GetType())</c> agrees on six of the same shapes and <b>diverges on
///         the seventh</b>: <c>x.GetType()</c> throws <c>NullReferenceException</c> where
///         <c>x is T</c> is <c>false</c>. A rule carries one safety answer, so the pair takes the
///         weaker one.
///     </para>
///     <para>
///         ⚠ <b>The nullable-value-type divergence this rule was expected to have does not exist.</b>
///         <c>Type.IsAssignableFrom</c> documents — and the probe confirms — a special case for a
///         nullable value type and its underlying type: <c>typeof(int?).IsAssignableFrom(typeof(int))</c>
///         is <c>true</c>, and <c>(object)someInt is int?</c> is <c>true</c> as well. The two agree,
///         and the belief that they did not is refuted.
///     </para>
///     <para>
///         ⚠ <b>Three types make <c>typeof(T)</c> compile and <c>x is T</c> not compile</b>, and all
///         three were confirmed against the compiler rather than assumed: a static class, a
///         <c>ref struct</c>, and an unbound generic such as <c>typeof(List&lt;&gt;)</c>. Each is a
///         legal reflection call and an uncompilable pattern, so each is declined.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectiveTypeTestAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ReflectiveTypeTest);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReflectiveTypeTest);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: TypeOfExpressionSyntax target
            } access
            || invocation.ArgumentList.Arguments.Count != 1) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        var isInstanceOfType = string.Equals(name, "IsInstanceOfType", System.StringComparison.Ordinal);
        if (!isInstanceOfType && !string.Equals(name, "IsAssignableFrom", System.StringComparison.Ordinal)) {
            return;
        }

        var argument = invocation.ArgumentList.Arguments[0];

        // ⚠ A named or `ref`/`in` argument is not this shape and its rewrite is not this rewrite.
        if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != (int)SyntaxKind.None) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol { ContainingType: { } declaring }
            || !IsSystemType(declaring)) {
            return;
        }

        // The value whose type is being tested. For `IsInstanceOfType` it is the argument itself;
        // for `IsAssignableFrom` the argument has to be a `GetType()` call, and the value is that
        // call's receiver. ⚠ `typeof(A).IsAssignableFrom(typeof(B))` is a question about two types
        // with no value in it at all, and has no `is` spelling — requiring `GetType()` declines it
        // by construction rather than by a name list.
        ExpressionSyntax operand;
        if (isInstanceOfType) {
            operand = argument.Expression;
        } else if (!TryReadGetTypeReceiver(model, argument.Expression, cancellation, out var receiver)) {
            return;
        } else {
            operand = receiver;
        }

        if (!IsPrimary(operand)) {
            return;
        }

        var tested = model.GetTypeInfo(target.Type, cancellation).Type;
        var operandType = model.GetTypeInfo(operand, cancellation).Type;
        if (!IsPatternableTarget(tested) || !IsPatternableOperand(operandType)) {
            return;
        }

        // ⚠ CS8121: a pattern whose type cannot be reached from the operand's type at all is a
        // compile error, not a false finding — `string s; s is int`. The reflection call is legal
        // there and always false, so the shape really occurs.
        if (!context.Compilation.ClassifyConversion(operandType!, tested!).Exists) {
            return;
        }

        var span = invocation.Span;
        if (RewriteGuards.ContainsCommentOrDirective(invocation.SyntaxTree, span)) {
            return;
        }

        // ⚠ The type is re-spelled from the `typeof` operand's own source text rather than from the
        // symbol. That text already binds to that type at this position — it is what the `typeof`
        // is doing — so the edit needs no `using` the file does not have and no qualification the
        // file does not use.
        var test = operand + " is " + target.Type;

        // ⚠ `is` binds looser than every unary and primary operator, so the replacement needs
        // parentheses wherever the call was an operand of one. `!typeof(T).IsInstanceOfType(x)`
        // becomes `!(x is T)`; without them it parses as `(!x) is T` and does not compile.
        var replacement = invocation.Parent is ExpressionSyntax and not ParenthesizedExpressionSyntax
            ? "(" + test + ")"
            : test;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((span, replacement)),
                "The `is` operator answers this directly: `" + RewriteGuards.Trim(test) + "`"
            )
        );
    }

    /// <summary>The receiver of an <c>object.GetType()</c> call, when that is what this is.</summary>
    static bool TryReadGetTypeReceiver(
        SemanticModel model,
        ExpressionSyntax expression,
        CancellationToken cancellation,
        out ExpressionSyntax receiver
    ) {
        receiver = expression;
        if (expression is not InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
            } call
            || !string.Equals(access.Name.Identifier.ValueText, "GetType", System.StringComparison.Ordinal)) {
            return false;
        }

        if (model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol {
                Name: "GetType",
                Parameters.Length: 0
            } method
            || method.ContainingType?.SpecialType != SpecialType.System_Object) {
            return false;
        }

        // ⚠ `SK2181` owns a `GetType()` whose receiver is already a `Type`, and that overlap is
        // real rather than theoretical: `typeof(Type).IsAssignableFrom(t.GetType())` satisfies both
        // shapes at once. It is a defect there — the call returns `System.RuntimeType` for every
        // input — not an opportunity to modernise, and the two rules offer contradictory edits, so
        // this one declines and leaves the expression to the rule that reports what is wrong with it.
        if (model.GetTypeInfo(access.Expression, cancellation).Type is { } receiverType
            && IsSystemType(receiverType)) {
            return false;
        }

        receiver = access.Expression;
        return true;
    }

    static bool IsSystemType(ITypeSymbol type) =>
        string.Equals(type.ToDisplayString(), "System.Type", System.StringComparison.Ordinal);

    /// <summary>Whether <c>x is T</c> compiles at all for this <c>T</c>.</summary>
    /// <remarks>
    ///     ⚠ Every exclusion here is a shape the compiler accepts as a <c>typeof</c> and rejects as a
    ///     pattern, confirmed by compiling all three: a static class, a <c>ref struct</c> and an
    ///     unbound generic. A rule that emitted any of them would produce a fix that does not build.
    /// </remarks>
    static bool IsPatternableTarget(ITypeSymbol? type) {
        if (type is null || type.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer
            or TypeKind.FunctionPointer) {
            return false;
        }

        if (type.SpecialType == SpecialType.System_Void) {
            return false;
        }

        if (type is INamedTypeSymbol named && (named.IsStatic || named.IsUnboundGenericType)) {
            return false;
        }

        return !type.IsRefLikeType;
    }

    /// <summary>
    ///     ⚠ Whether the operand can be pattern-tested without the compiler warning that the answer is
    ///     already known.
    /// </summary>
    /// <remarks>
    ///     A reference-typed operand can always be null, so <c>x is T</c> is never CS0183 "the given
    ///     expression is always of the provided type". A value-typed one frequently is —
    ///     <c>int n; n is object</c> — and the fixture harness' re-binding test treats an
    ///     introduced warning as a broken fix, correctly. The reflection call on a value operand
    ///     boxes and is a different (and rarer) shape; it is declined rather than reported without a
    ///     usable edit.
    /// </remarks>
    static bool IsPatternableOperand(ITypeSymbol? type) =>
        type is not null
        && type.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer)
        && type.IsReferenceType;

    /// <summary>
    ///     ⚠ Whether the operand can stand to the left of <c>is</c> without parentheses of its own.
    /// </summary>
    /// <remarks>
    ///     The rewrite moves the expression from an argument position, where every precedence is
    ///     legal, to the left of a relational operator, where most are not: <c>a ? b : c is T</c> does
    ///     not mean <c>(a ? b : c) is T</c>. Rather than parenthesise an arbitrary expression, the
    ///     rule matches only the primary forms, which cover the shape as it is actually written and
    ///     need nothing added.
    /// </remarks>
    static bool IsPrimary(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax
            or ThisExpressionSyntax
            or BaseExpressionSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or LiteralExpressionSyntax
            or ParenthesizedExpressionSyntax
            or ObjectCreationExpressionSyntax;
}
