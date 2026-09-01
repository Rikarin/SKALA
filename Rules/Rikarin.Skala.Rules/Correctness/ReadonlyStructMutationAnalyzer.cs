using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>SK2005: a mutating struct method operates on a defensive copy of a readonly field.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyStructMutationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReadonlyStructMutation);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            || model.GetSymbolInfo(PatternSafety.Unwrap(access.Expression), cancellation).Symbol is not IFieldSymbol {
                IsReadOnly: true,
                Type.TypeKind: TypeKind.Struct
            }
            || model.GetOperation(context.Node, cancellation) is not IInvocationOperation {
                Instance: IFieldReferenceOperation { Field.IsReadOnly: true } receiver,
                TargetMethod:
                {
                    IsReadOnly: false,
                    IsStatic: false,
                    ReturnsVoid: true,
                    ContainingType: { TypeKind: TypeKind.Struct, IsReadOnly: false }
                } method
            }
            || receiver.Field.RefKind != RefKind.None
            || method.DeclaringSyntaxReferences.Length != 1
            || method.DeclaringSyntaxReferences[0].SyntaxTree != context.Node.SyntaxTree
            || method.GetAttributes()
                .Any(static attribute => attribute.AttributeClass?.ToDisplayString()
                    == "System.Diagnostics.ConditionalAttribute"
                )
            || NullComparison.InsideExpressionTree(model, context.Node, cancellation)
            || method.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not MethodDeclarationSyntax declaration) {
            return;
        }

        // The declaring constructor may write its own readonly storage without making a copy.
        if (context.Node.Ancestors()
                .OfType<ConstructorDeclarationSyntax>()
                .Any(constructor =>
                    model.GetDeclaredSymbol(constructor, cancellation) is { } symbol
                    && SymbolEqualityComparer.Default.Equals(
                        symbol.ContainingType.OriginalDefinition,
                        receiver.Field.ContainingType.OriginalDefinition
                    )
                )) {
            return;
        }

        if (context.Node.Ancestors()
                .OfType<AccessorDeclarationSyntax>()
                .Any(accessor => accessor.IsKind(SyntaxKind.InitAccessorDeclaration)
                    && model.GetDeclaredSymbol(accessor, cancellation) is { } symbol
                    && SymbolEqualityComparer.Default.Equals(
                        symbol.ContainingType.OriginalDefinition,
                        receiver.Field.ContainingType.OriginalDefinition
                    )
                )) {
            return;
        }

        var expressions = declaration.Body is { } body
            ? body.Statements.OfType<ExpressionStatementSyntax>().Select(static statement => statement.Expression)
            : declaration.ExpressionBody is { } arrow
                ? new[] { arrow.Expression }
                : Enumerable.Empty<ExpressionSyntax>();
        if (!expressions.Any(expression => {
                    var target = model.GetOperation(expression, cancellation) switch {
                        IAssignmentOperation assignment => assignment.Target,
                        IIncrementOrDecrementOperation increment => increment.Target,
                        _ => null
                    };
                    return target is IFieldReferenceOperation {
                        Field.IsStatic: false,
                        Instance:
                        IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
                    };
                }
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                context.Node.GetLocation(),
                "This struct call writes to a defensive copy, not the readonly field `" + receiver.Field.Name + "`"
            )
        );
    }
}
