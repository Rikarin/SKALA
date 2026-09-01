using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1025: freeze a private static lookup table whose complete usage stays read-only.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FrozenDictionaryAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FrozenDictionary);

    static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
        SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
        | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (FieldDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (!PrivateFieldUsage.TryRead(
                model,
                declaration,
                cancellation,
                static candidate => candidate.IsStatic
                    && candidate.IsReadOnly
                    && candidate.Type is INamedTypeSymbol { Name: "Dictionary", TypeArguments.Length: 2 },
                out var field,
                out var uses
            )
            || field.Type is not INamedTypeSymbol { TypeArguments.Length: 2 } type
            || type.TypeArguments[0].NullableAnnotation == NullableAnnotation.Annotated
            || PrivateFieldUsage.FrameworkType(
                model.Compilation,
                "System.Collections.Generic.Dictionary`2"
            ) is not { } dictionary
            || !SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, dictionary)
            || !(PatternSafety.IsIntegral(type.TypeArguments[0])
                || type.TypeArguments[0].SpecialType == SpecialType.System_String)
            || PrivateFieldUsage.FrameworkType(
                model.Compilation,
                "System.Collections.Frozen.FrozenDictionary`2"
            ) is not { } frozen
            || PrivateFieldUsage.FrameworkType(
                model.Compilation,
                "System.Collections.Frozen.FrozenDictionary"
            ) is not { } factory
            || declaration.Declaration.Variables[0].Initializer?.Value is not BaseObjectCreationExpressionSyntax {
                Initializer: { Expressions.Count: > 0 } initializer
            } creation
            || model.GetOperation(creation, cancellation) is not IObjectCreationOperation {
                Constructor.Parameters.Length: 0
            } constructed
            || !SymbolEqualityComparer.Default.Equals(constructed.Type, type)
            || !ConstantDependencies.AreFileLocal(model, creation, cancellation)
            || !initializer.Expressions.All(expression => expression is InitializerExpressionSyntax {
                    Expressions.Count: 2
                } item
                && item.Expressions.All(value => model.GetConstantValue(value, cancellation).HasValue)
            )) {
            return;
        }

        foreach (var use in uses) {
            if (NullComparison.InsideExpressionTree(model, use, cancellation)) {
                return;
            }

            SyntaxNode expression = use;
            while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses;
            }

            if (expression.Parent is ElementAccessExpressionSyntax indexer
                && indexer.Expression == expression
                && model.GetOperation(indexer, cancellation) is IPropertyReferenceOperation { Property.IsIndexer: true }
                && !IsWrite(indexer)
                && !CallerArgumentSafety.CapturesText(model, indexer, cancellation)) {
                continue;
            }

            if (expression.Parent is MemberAccessExpressionSyntax access
                && access.Expression == expression
                && model.GetSymbolInfo(access, cancellation).Symbol is IPropertySymbol { Name: "Count" }) {
                continue;
            }

            if (expression.Parent is MemberAccessExpressionSyntax member
                && member.Expression == expression
                && member.Parent is InvocationExpressionSyntax invocation
                && model.GetOperation(invocation, cancellation) is IInvocationOperation call
                && call.TargetMethod.Name is "ContainsKey" or "TryGetValue"
                && SymbolEqualityComparer.Default.Equals(
                    call.TargetMethod.ContainingType.OriginalDefinition,
                    dictionary
                )
                && !NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
                continue;
            }

            return;
        }

        // Keep the Dictionary construction (including duplicate-key validation), then freeze that exact table.
        var original = "new " + type.ToDisplayString(TypeFormat) + "() " + initializer;
        var replacement = SyntaxFactory.ParseExpression(
            "global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(" + original + ")"
        );
        var frozenType = frozen.Construct(type.TypeArguments.ToArray());
        if (model.GetSpeculativeSymbolInfo(
                creation.SpanStart,
                replacement,
                SpeculativeBindingOption.BindAsExpression
            ).Symbol
            is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, factory)
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, frozenType)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Declaration.Variables[0].Identifier.GetLocation(),
                FixEdits.Pack(
                    (declaration.Declaration.Type.Span,
                        frozenType.ToDisplayString(TypeFormat)),
                    (creation.Span, replacement.ToString())
                ),
                "Freeze this private, constant lookup-only dictionary"
            )
        );
    }

    static bool IsWrite(ExpressionSyntax expression) {
        while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
            expression = parentheses;
        }

        return expression.Ancestors()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left.Span.Contains(expression.Span))
            || expression.Parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax or RefExpressionSyntax;
    }
}
