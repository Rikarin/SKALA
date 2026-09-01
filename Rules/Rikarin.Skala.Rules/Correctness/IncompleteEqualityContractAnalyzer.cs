using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary><c>SK2004</c> — typed equality with no object equality contract.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IncompleteEqualityContractAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.IncompleteEqualityContract);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol { IsRecord: false } type
            || type.DeclaringSyntaxReferences.FirstOrDefault() is not { } first
            || first.SyntaxTree != declaration.SyntaxTree
            || first.Span != declaration.Span) {
            return;
        }

        var equatable = context.Compilation.GetTypeByMetadataName("System.IEquatable`1");
        if (equatable is null
            || !type.AllInterfaces.Any(contract =>
                SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, equatable)
                && SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], type)
            )
            || HasObjectEquals(type)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + type.Name
                + "` implements typed equality but not object equality; align Equals(object) and GetHashCode"
            )
        );
    }

    static bool HasObjectEquals(INamedTypeSymbol type) {
        for (var current = type;
             current is not null
             && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType) {
            foreach (var method in current.GetMembers("Equals").OfType<IMethodSymbol>()) {
                if (method.IsOverride
                    && method.Parameters.Length == 1
                    && method.Parameters[0].Type.SpecialType == SpecialType.System_Object
                    && method.ReturnType.SpecialType == SpecialType.System_Boolean) {
                    return true;
                }
            }
        }

        return false;
    }
}
