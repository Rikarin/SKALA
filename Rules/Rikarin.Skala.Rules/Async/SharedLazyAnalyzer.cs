using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>SK3009: an explicitly unsynchronized Lazy initializer stored in static state.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedLazyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SharedLazy);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.VariableDeclarator);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var variable = (VariableDeclaratorSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (variable.Parent?.Parent is not FieldDeclarationSyntax
            || variable.Initializer?.Value is not BaseObjectCreationExpressionSyntax creation
            || model.GetDeclaredSymbol(variable, cancellation) is not IFieldSymbol { IsStatic: true } field
            || field.GetAttributes()
                .Any(static attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute"
                )
            || model.GetOperation(creation, cancellation) is not IObjectCreationOperation {
                Constructor: { } constructor
            } operation) {
            return;
        }

        var lazy = context.Compilation.GetTypeByMetadataName("System.Lazy`1");
        if (lazy is null
            || lazy.Locations.Any(static location => location.IsInSource)
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType.OriginalDefinition, lazy)) {
            return;
        }

        var mode = context.Compilation.GetTypeByMetadataName("System.Threading.LazyThreadSafetyMode");
        foreach (var argument in operation.Arguments) {
            if (argument.IsImplicit
                || argument.Parameter is not { } parameter
                || !ConstantDependencies.AreFileLocal(model, argument.Value.Syntax, cancellation)) {
                continue;
            }

            var value = argument.Value.ConstantValue;
            if (value.HasValue
                && (parameter.Type.SpecialType == SpecialType.System_Boolean
                    && value.Value is false
                    || SymbolEqualityComparer.Default.Equals(parameter.Type, mode)
                    && value.Value is int number
                    && number == 0)) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        argument.Syntax.GetLocation(),
                        "Static Lazy<T> field `"
                        + field.Name
                        + "` disables thread safety; ensure single-threaded access or external synchronization"
                    )
                );
                return;
            }
        }
    }
}
