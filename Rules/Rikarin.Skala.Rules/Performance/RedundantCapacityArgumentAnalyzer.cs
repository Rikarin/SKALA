using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4023</c> — a <c>capacity</c> argument that asks for the capacity the type already has.
/// </summary>
/// <remarks>
///     ⚠ A closed table rather than a heuristic. "The default capacity" is a fact about six framework
///     types and about nothing else: a type of the author's own may document any default it likes and
///     change it in the next commit, so a rule that guessed from the parameter name would be deleting
///     an argument on the strength of a coincidence.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantCapacityArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantCapacityArgument);

    /// <summary>The metadata name of a framework collection, and the capacity its default gives.</summary>
    static readonly Dictionary<string, int> Defaults = new(System.StringComparer.Ordinal) {
        ["System.Collections.Generic.List`1"] = 0,
        ["System.Collections.Generic.Dictionary`2"] = 0,
        ["System.Collections.Generic.HashSet`1"] = 0,
        ["System.Collections.Generic.Queue`1"] = 0,
        ["System.Collections.Generic.Stack`1"] = 0,
        ["System.Text.StringBuilder"] = 16
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        var cancellation = context.CancellationToken;
        if (creation.ArgumentList is not { Arguments.Count: 1 } arguments
            || arguments.Arguments[0] is not { Expression: LiteralExpressionSyntax literal } argument
            || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
            || !literal.IsKind(SyntaxKind.NumericLiteralExpression)
            || RewriteGuards.ContainsCommentOrDirective(arguments)
            || context.SemanticModel.GetSymbolInfo(creation, cancellation).Symbol is not IMethodSymbol {
                MethodKind: MethodKind.Constructor, Parameters.Length: 1
            } constructor
            || constructor.Parameters[0] is not {
                Name: "capacity", Type.SpecialType: SpecialType.System_Int32
            }) {
            return;
        }

        var type = constructor.ContainingType;
        if (!Defaults.TryGetValue(MetadataName(type), out var standard)
            || type.Locations.Any(static location => location.IsInSource)
            || context.SemanticModel.GetConstantValue(literal, cancellation) is not { HasValue: true, Value: int written }
            || written != standard
            || !type.InstanceConstructors.Any(static candidate => candidate.Parameters.Length == 0
                && candidate.DeclaredAccessibility == Accessibility.Public
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                arguments.GetLocation(),
                FixEdits.Pack((arguments.Span, "()")),
                "`"
                + type.Name
                + "`'s default capacity is already "
                + standard.ToString(CultureInfo.InvariantCulture)
                + "; the argument decides nothing"
            )
        );
    }

    static string MetadataName(INamedTypeSymbol type) {
        var definition = type.OriginalDefinition;
        var name = definition.MetadataName;
        var container = definition.ContainingType is null
            ? definition.ContainingNamespace?.ToDisplayString()
            : null;
        return string.IsNullOrEmpty(container) ? name : container + "." + name;
    }
}
