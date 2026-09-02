using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2182</c> — a type identified by comparing <c>GetType().Name</c> against a string literal.
/// </summary>
/// <remarks>
///     <para>
///         The line reads as a type test and is a string test. A rename updates every reference in the
///         solution except this one, which keeps compiling and starts returning <c>false</c> — a
///         branch that quietly stops being taken, which is the hardest failure there is to trace back
///         to its cause. In the other direction <c>Type.Name</c> is not unique: any type of that name
///         in any namespace of any referenced assembly matches it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The literal must name a type this compilation can already see, and that one condition
///             is the whole specification.
///         </b> A name comparison is the <em>only</em> option for a type
///         loaded reflectively, for a plugin whose assembly is deliberately not referenced, and across
///         a boundary this project does not compile against — and in every one of those the name does
///         not resolve here, so nothing is reported. Where it does resolve, the file could have named
///         the type outright and chose a string instead.
///     </para>
///     <para>
///         ⚠ <b><c>AssemblyQualifiedName</c> is never reported.</b> It carries a version, a culture and
///         a public key token, so comparing it is a statement about which <em>build</em> of a type this
///         is — something <c>typeof(T)</c> cannot say. <c>Name</c>, <c>FullName</c> and
///         <c>AssemblyQualifiedName</c> are three different questions and only two have a symbol
///         answer.
///     </para>
///     <para>
///         ⚠ <b>The fix is <c>GetType() == typeof(T)</c> and never <c>is T</c>.</b> A name comparison
///         is exact; <c>is</c> matches subclasses too, so that rewrite would change behaviour in a way
///         the string comparison never had.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeComparedByNameAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TypeComparedByName);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeComparison,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression
        );

        context.RegisterSyntaxNodeAction(AnalyzeEqualsCall, SyntaxKind.InvocationExpression);
    }

    static void AnalyzeComparison(SyntaxNodeAnalysisContext context) {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (comparison.ContainsDiagnostics) {
            return;
        }

        var negated = comparison.IsKind(SyntaxKind.NotEqualsExpression);
        if (Literal(comparison.Right) is { } right) {
            Report(context, comparison, comparison.Left, right, negated);
        } else if (Literal(comparison.Left) is { } left) {
            Report(context, comparison, comparison.Right, left, negated);
        }
    }

    /// <summary>
    ///     <c>x.GetType().Name.Equals("Order", StringComparison.Ordinal)</c>, the same defect spelled
    ///     as a call.
    /// </summary>
    static void AnalyzeEqualsCall(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ContainsDiagnostics
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Name.Identifier.ValueText != "Equals"
            || invocation.ArgumentList.Arguments.Count is not (1 or 2)
            || Literal(invocation.ArgumentList.Arguments[0].Expression) is not { } literal) {
            return;
        }

        // ⚠ Only the two-argument form carrying a `StringComparison` and the one-argument form are
        // this shape. Anything else is a different `Equals`.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol {
                ContainingType.SpecialType: SpecialType.System_String
            }) {
            return;
        }

        Report(context, invocation, access.Expression, literal, false);
    }

    static string? Literal(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal
        && literal.Token.ValueText.Length > 0
            ? literal.Token.ValueText
            : null;

    static void Report(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax whole,
        ExpressionSyntax nameSide,
        string literal,
        bool negated
    ) {
        if (nameSide is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } property) {
            return;
        }

        var kind = property.Name.Identifier.ValueText;
        if (kind is not ("Name" or "FullName")) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The receiver must be a `GetType()` call. `someType.Name == "Order"` on a `Type` variable
        // has no `typeof` rewrite — the subject there is a type, not an instance — so it is declined
        // rather than reported without an answer.
        if (property.Expression is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } call
            || call.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } getType
            || getType.Name.Identifier.ValueText != "GetType"
            || model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol {
                Parameters.Length: 0,
                IsStatic: false
            }) {
            return;
        }

        // ⚠ The subject is the *call's* type rather than the property's containing type. `Type.Name`
        // overrides `MemberInfo.Name`, so the declaring symbol reached from a `Type` receiver is not
        // reliably `System.Type` itself — and testing for that equality silenced this rule on every
        // one of its own fixtures before the suite caught it.
        if (model.Compilation.GetTypeByMetadataName("System.Type") is not { } systemType
            || model.GetTypeInfo(call, cancellation).Type is not { } produced
            || !IsOrDerivesFrom(produced, systemType)
            || model.GetSymbolInfo(property, cancellation).Symbol is not IPropertySymbol { IsStatic: false }) {
            return;
        }

        if (Resolve(model.Compilation, kind, literal, cancellation) is not { } named) {
            return;
        }

        var subject = getType.Expression.ToString();
        var replacement = subject
            + ".GetType() "
            + (negated ? "!=" : "==")
            + " typeof("
            + TypeNameWriting.At(named, model, whole.SpanStart)
            + ")";

        var properties = RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(whole.SyntaxTree, whole.Span)
            ? null
            : FixEdits.Pack((whole.Span, replacement));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                whole.GetLocation(),
                properties,
                "`"
                + literal
                + "` names `"
                + named.ToDisplayString()
                + "`, which this file can already name, so the comparison is a string the compiler "
                + "never checks and a rename will silently falsify"
            )
        );
    }

    /// <summary>
    ///     The type the literal names, or <c>null</c> where nothing here can name it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The two properties resolve differently on purpose. <c>FullName</c> is a metadata name, so
    ///     it goes through <c>GetTypeByMetadataName</c> and reaches referenced assemblies —
    ///     a fully-qualified name that resolves is a type the file could have written. <c>Name</c> is a
    ///     simple name and is only resolved against this compilation's <em>own</em> declarations, which
    ///     is deliberately the narrower search: a simple name matching some type in some referenced
    ///     assembly proves nothing about what the author meant, and the plugin and reflection cases
    ///     this rule must decline are exactly the ones where the name is not declared here.
    /// </remarks>
    static INamedTypeSymbol? Resolve(
        Compilation compilation,
        string kind,
        string literal,
        CancellationToken cancellation
    ) {
        if (kind == "FullName") {
            return compilation.GetTypeByMetadataName(literal);
        }

        INamedTypeSymbol? found = null;
        foreach (var symbol in compilation.GetSymbolsWithName(
                     name => name == literal,
                     SymbolFilter.Type,
                     cancellation
                 )) {
            if (symbol is not INamedTypeSymbol named) {
                continue;
            }

            // ⚠ Two source types of one simple name make the literal genuinely ambiguous, and a fix
            // would have to pick one. Nothing is reported.
            if (found is not null && !SymbolEqualityComparer.Default.Equals(found, named)) {
                return null;
            }

            found = named;
        }

        // A generic type's metadata name carries the arity, so `Order` never resolves to `Order<T>`
        // and a `typeof` of it would need type arguments this rule cannot invent.
        return found is { IsGenericType: false } && found.Name == literal ? found : null;
    }

    static bool IsOrDerivesFrom(ITypeSymbol candidate, INamedTypeSymbol target) {
        for (var current = candidate; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, target)) {
                return true;
            }
        }

        return false;
    }
}
