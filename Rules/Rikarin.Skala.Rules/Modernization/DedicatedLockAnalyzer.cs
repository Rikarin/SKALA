using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1023: a private, readonly object used exclusively as a lock target.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DedicatedLockAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DedicatedLock);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var type = start.Compilation.GetTypeByMetadataName("System.Threading.Lock");
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, "13.0")
                    || type is null
                    || type.Locations.Any(static location => location.IsInSource)
                    || !type.InstanceConstructors.Any(static constructor => constructor.DeclaredAccessibility
                        == Accessibility.Public
                        && constructor.Parameters.Length == 0
                    )
                    || !type.GetMembers("EnterScope")
                        .OfType<IMethodSymbol>()
                        .Any(method => !method.IsStatic
                            && method.DeclaredAccessibility == Accessibility.Public
                            && method.Parameters.Length == 0
                            && method.ReturnType is INamedTypeSymbol { Name: "Scope", IsRefLikeType: true } scope
                            && SymbolEqualityComparer.Default.Equals(scope.ContainingType, type)
                            && scope.GetMembers("Dispose")
                                .OfType<IMethodSymbol>()
                                .Any(static dispose => !dispose.IsStatic
                                    && dispose.DeclaredAccessibility == Accessibility.Public
                                    && dispose.Parameters.Length == 0
                                    && dispose.ReturnsVoid
                                )
                        )) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (declaration.Parent is not ClassDeclarationSyntax
            || declaration.Declaration.Variables.Count != 1
            || declaration.AttributeLists.Count != 0
            || declaration.SpanContainsComment()
            || declaration.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static type => type.Modifiers.Any(SyntaxKind.PartialKeyword))) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        var variable = declaration.Declaration.Variables[0];
        if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax { Initializer: null } creation
            || model.GetDeclaredSymbol(variable, cancellation) is not IFieldSymbol {
                DeclaredAccessibility: Accessibility.Private,
                IsReadOnly: true,
                Type.SpecialType: SpecialType.System_Object
            } field
            || model.GetSymbolInfo(creation, cancellation).Symbol is not IMethodSymbol {
                Parameters.Length: 0,
                ContainingType.SpecialType: SpecialType.System_Object
            }) {
            return;
        }

        var root = declaration.SyntaxTree.GetRoot(cancellation);
        // Inactive references and conditional fields must not escape the whole-file reference proof.
        if (root.ContainsDirectives) {
            return;
        }

        var count = 0;
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (name.Identifier.ValueText != field.Name
                || model.GetSymbolInfo(name, cancellation).Symbol is not IFieldSymbol reference
                || !SymbolEqualityComparer.Default.Equals(field.OriginalDefinition, reference.OriginalDefinition)) {
                continue;
            }

            SyntaxNode expression = name;
            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name) {
                expression = access;
            }

            while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses;
            }

            if (expression.Parent is not LockStatementSyntax statement
                || statement.Expression != expression
                || statement.Statement.DescendantNodesAndSelf().OfType<YieldStatementSyntax>().Any()) {
                return;
            }

            count++;
        }

        if (count == 0) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                variable.Identifier.GetLocation(),
                FixEdits.Pack(
                    (declaration.Declaration.Type.Span, "global::System.Threading.Lock"),
                    (creation.Span, "new global::System.Threading.Lock()")
                ),
                "Use System.Threading.Lock for this private synchronization-only field"
            )
        );
    }
}
