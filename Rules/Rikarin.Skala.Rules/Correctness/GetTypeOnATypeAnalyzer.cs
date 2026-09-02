using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2181</c> — <c>GetType()</c> called on a receiver that is already a <see cref="System.Type" />.
/// </summary>
/// <remarks>
///     <para>
///         The call returns <c>System.RuntimeType</c> for every input. It is never null, it is a
///         <c>Type</c>, and two calls agree with each other — which is why the mistake survives every
///         test that checks any of those three things. A dictionary keyed on it has one key.
///     </para>
///     <para>
///         ⚠ <b>Nothing else reports it.</b> Probed outside this repository at
///         <c>AnalysisMode=All</c> with <c>EnforceCodeStyleInBuild</c>: <c>t.GetType()</c> on a
///         <c>Type</c> parameter and <c>typeof(Widget).GetType()</c> both produce no compiler
///         diagnostic and no <c>CA*</c> diagnostic at all.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Asking for the runtime type of a <c>Type</c> object is a real question and is
///             declined by recognising it.
///         </b> Reflection-emit code separates a <c>RuntimeType</c> from a
///         <c>TypeBuilder</c> or a <c>TypeDelegator</c>, and it does so by testing the result against a
///         type that itself derives from <c>System.Type</c>. That test is silent here. The other escape
///         hatch is the documented one — <c>((object)t).GetType()</c> — which the rule does not look
///         through, because it reads the receiver's <em>static</em> type and nothing else.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GetTypeOnATypeAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.GetTypeOnAType);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ContainsDiagnostics
            || invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access
            || access.Name.Identifier.ValueText != "GetType") {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.Compilation.GetTypeByMetadataName("System.Type") is not { } systemType) {
            return;
        }

        // ⚠ **`System.Type` declares its own parameterless `GetType()`**, hiding `object`'s, so a call
        // on a `Type` receiver binds there and not to `object.GetType()`. Testing the containing type
        // for `System_Object` — the obvious spelling — silences this rule on every fixture it exists
        // for, which is how it was found.
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                Parameters.Length: 0,
                IsStatic: false,
                ReturnType: { } returned
            }
            || !IsOrDerivesFrom(returned, systemType)) {
            return;
        }

        if (model.GetTypeInfo(access.Expression, cancellation).Type is not { } receiver
            || receiver.TypeKind is TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter or TypeKind.Unknown
            || !IsOrDerivesFrom(receiver, systemType)) {
            return;
        }

        if (AsksForTheRuntimeTypeOfTheTypeObject(invocation, model, systemType, cancellation)) {
            return;
        }

        // ⚠ The edit removes the call and keeps the receiver's own text, so the name that stays is the
        // one that was already in scope. It is withheld where a comment or a directive sits inside the
        // span — `RewriteGuards.ContainsCommentOrDirective` and not a trivia walk above the node,
        // which is what issue #302 records going wrong.
        var span = invocation.Span;
        var properties = RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(invocation.SyntaxTree, span)
            ? null
            : FixEdits.Pack((span, access.Expression.ToString()));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                access.Name.GetLocation(),
                properties,
                "`"
                + receiver.ToDisplayString()
                + "` is already a type, so this returns the runtime type of the `Type` object rather than "
                + "the type it describes"
            )
        );
    }

    /// <summary>
    ///     Whether the result is being compared against a type that is itself a <c>Type</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the whole false-positive story. <c>t.GetType() == typeof(TypeBuilder)</c> and
    ///     <c>t.GetType() is TypeDelegator</c> are the reflection-emit idiom, and their author means
    ///     exactly what is written. The test is on the <em>compared</em> type deriving from
    ///     <c>System.Type</c>, because comparing against anything else cannot be that question.
    /// </remarks>
    static bool AsksForTheRuntimeTypeOfTheTypeObject(
        ExpressionSyntax invocation,
        SemanticModel model,
        INamedTypeSymbol systemType,
        System.Threading.CancellationToken cancellation
    ) {
        switch (invocation.Parent) {
            case BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression
            } comparison: {
                var other = comparison.Left == invocation ? comparison.Right : comparison.Left;
                return other is TypeOfExpressionSyntax typeOf
                    && model.GetTypeInfo(typeOf.Type, cancellation).Type is { } compared
                    && IsOrDerivesFrom(compared, systemType);
            }

            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression or (int)SyntaxKind.AsExpression } test:
                return model.GetTypeInfo(test.Right, cancellation).Type is { } tested
                    && IsOrDerivesFrom(tested, systemType);

            case IsPatternExpressionSyntax { Pattern: var pattern }:
                return NamedTypeOf(pattern) is { } named
                    && model.GetTypeInfo(named, cancellation).Type is { } patternType
                    && IsOrDerivesFrom(patternType, systemType);

            default:
                return false;
        }
    }

    static TypeSyntax? NamedTypeOf(PatternSyntax pattern) =>
        pattern switch {
            TypePatternSyntax type => type.Type,
            DeclarationPatternSyntax declaration => declaration.Type,
            RecursivePatternSyntax { Type: { } type } => type,
            UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } negated => NamedTypeOf(negated.Pattern),
            _ => null
        };

    static bool IsOrDerivesFrom(ITypeSymbol candidate, INamedTypeSymbol target) {
        for (var current = candidate; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, target)) {
                return true;
            }
        }

        return false;
    }
}
