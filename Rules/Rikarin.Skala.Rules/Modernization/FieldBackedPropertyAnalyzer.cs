using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1003: a private field exclusively owned by one property's accessors.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldBackedPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.FieldBackedProperty);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "14.0")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (FieldDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (declaration.Declaration.Variables.Any(static variable => variable.Initializer is not null)
            || declaration.Parent is not ClassDeclarationSyntax owner
            || !owner.Members.OfType<PropertyDeclarationSyntax>()
                .Any(static property => property.AccessorList is { Accessors.Count: 2 })
            || !PrivateFieldUsage.TryRead(
                model,
                declaration,
                cancellation,
                static candidate => !candidate.IsReadOnly
                    && !candidate.IsVolatile
                    && !candidate.IsConst
                    && candidate.RefKind == RefKind.None
                    && candidate.Type.TypeKind != TypeKind.TypeParameter
                    && (!candidate.Type.IsReferenceType
                        || candidate.NullableAnnotation == NullableAnnotation.Annotated),
                out var field,
                out var uses
            )) {
            return;
        }

        var property = uses[0].Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (property?.AccessorList is not { Accessors.Count: 2 } accessors
            || property.Parent != declaration.Parent
            || property.Initializer is not null
            || property.AttributeLists.Count != 0
            || property.ContainsDirectives
            || !accessors.Accessors.Any(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            || !accessors.Accessors.Any(static accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            || accessors.Accessors.Any(static accessor => accessor.Body is null && accessor.ExpressionBody is null)
            || model.GetDeclaredSymbol(property, cancellation) is not IPropertySymbol {
                RefKind: RefKind.None,
                IsAbstract: false,
                IsVirtual: false,
                IsOverride: false
            } symbol
            || symbol.IsStatic != field.IsStatic
            || !SymbolEqualityComparer.IncludeNullability.Equals(symbol.Type, field.Type)
            || accessors.Accessors.Any(accessor => !uses.Any(use => accessor.Span.Contains(use.Span)))
            || symbol.ContainingType.GetAttributes()
                .Any(static attribute => attribute.AttributeClass?.ToDisplayString()
                    is "System.Runtime.InteropServices.StructLayoutAttribute" or "System.SerializableAttribute"
                )
            || property.DescendantNodes()
                .Any(static node => node is AnonymousFunctionExpressionSyntax
                        or LocalFunctionStatementSyntax
                    || node is IdentifierNameSyntax { Identifier.ValueText: "field" }
                )) {
            return;
        }

        var edits = new List<(TextSpan, string)> { (declaration.Span, string.Empty) };
        foreach (var use in uses) {
            if (!accessors.Span.Contains(use.Span)
                || use is MemberAccessExpressionSyntax { Expression: not ThisExpressionSyntax }
                || use.Ancestors()
                    .Any(static node => node is RefExpressionSyntax or MakeRefExpressionSyntax
                        || node is InvocationExpressionSyntax {
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                        }
                    )
                || use.Ancestors()
                    .OfType<ArgumentSyntax>()
                    .Any(static argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None))
                || use.Parent is PrefixUnaryExpressionSyntax prefix
                && prefix.IsKind(SyntaxKind.AddressOfExpression)
                || CallerArgumentSafety.CapturesText(model, use, cancellation)
                || use.SpanContainsComment()) {
                return;
            }

            edits.Add((use.Span, "field"));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                property.Identifier.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "Use the compiler-backed field inside this property's accessors"
            )
        );
    }
}
