using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary><c>SK3502</c> — a type constructs a disposable field but is not disposable itself.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OwnedDisposableFieldAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OwnedDisposableField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (disposable is null && asyncDisposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, asyncDisposable),
                    SyntaxKind.FieldDeclaration
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? disposable,
        INamedTypeSymbol? asyncDisposable
    ) {
        var field = (FieldDeclarationSyntax)context.Node;
        if (field.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
            || field.Parent is not TypeDeclarationSyntax declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol owner) {
            return;
        }

        foreach (var variable in field.Declaration.Variables) {
            if (variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax
                || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                is not IFieldSymbol symbol
                || symbol.Type.TypeKind == TypeKind.Error) {
                continue;
            }

            var needsSync = disposable is not null && Implements(symbol.Type, disposable);
            var needsAsync = asyncDisposable is not null && Implements(symbol.Type, asyncDisposable);
            if (!needsSync
                && !needsAsync
                || needsSync
                && Implements(owner, disposable!)
                || needsAsync
                && Implements(owner, asyncDisposable!)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    variable.Identifier.GetLocation(),
                    "`"
                    + owner.Name
                    + "` constructs disposable field `"
                    + symbol.Name
                    + "` but does not expose matching disposal"
                )
            );
        }
    }

    static bool Implements(ITypeSymbol type, INamedTypeSymbol contract) {
        if (SymbolEqualityComparer.Default.Equals(type, contract)) {
            return true;
        }

        foreach (var candidate in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(candidate, contract)) {
                return true;
            }
        }

        return false;
    }
}
