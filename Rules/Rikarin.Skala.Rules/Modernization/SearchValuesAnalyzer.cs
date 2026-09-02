using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>SK1022: precompute a private constant character set used only by span searches.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SearchValuesAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SearchValues);
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, "7.2")) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
                }
            }
        );
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
                    && candidate.Type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Char },
                out _,
                out var uses
            )
            || declaration.Declaration.Variables[0].Initializer?.Value is not { } initializer
            || PrivateFieldUsage.FrameworkType(model.Compilation, "System.Buffers.SearchValues`1") is not { } search
            || PrivateFieldUsage.FrameworkType(model.Compilation, "System.Buffers.SearchValues") is not { } factory
            || PrivateFieldUsage.FrameworkType(model.Compilation, "System.MemoryExtensions") is not { } extensions
            || !ConstantDependencies.AreFileLocal(model, initializer, cancellation)) {
            return;
        }

        // Avoid reentrant type initialization observing a null array as an empty span but a null SearchValues as an error.
        var owner = (ClassDeclarationSyntax)declaration.Parent!;
        if (owner.Members.OfType<ConstructorDeclarationSyntax>()
                .Any(static constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            || owner.Members.OfType<FieldDeclarationSyntax>()
                .Any(other => other != declaration
                    && other.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && other.Declaration.Variables.Any(static variable => variable.Initializer is not null)
                )
            || owner.Members.OfType<PropertyDeclarationSyntax>()
                .Any(static property => property.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && property.Initializer is not null
                )) {
            return;
        }

        var values = Characters(model, initializer, cancellation);
        if (values is null || values.Length > 256 || values.Distinct().Count() < 4) {
            return;
        }

        var searchType = search.Construct(model.Compilation.GetSpecialType(SpecialType.System_Char));
        var creation = SyntaxFactory.ParseExpression(
            "global::System.Buffers.SearchValues.Create("
            + SyntaxFactory.Literal(values) + ")"
        );
        if (model.GetSpeculativeSymbolInfo(
                initializer.SpanStart,
                creation,
                SpeculativeBindingOption.BindAsExpression
            ).Symbol
            is not IMethodSymbol creator
            || !SymbolEqualityComparer.Default.Equals(creator.ContainingType, factory)
            || !SymbolEqualityComparer.Default.Equals(creator.ReturnType, searchType)) {
            return;
        }

        foreach (var use in uses) {
            SyntaxNode expression = use;
            while (expression.Parent is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses;
            }

            if (expression.Parent is not ArgumentSyntax { NameColon: null } argument
                || argument.Parent?.Parent is not InvocationExpressionSyntax invocation
                || invocation.ContainsDirectives
                || RewriteGuards.ContainsCommentOrDirective(invocation.SyntaxTree, invocation.Span)
                || model.GetOperation(invocation, cancellation) is not IInvocationOperation call
                || call.TargetMethod.Name is not ("IndexOfAny"
                    or "IndexOfAnyExcept"
                    or "ContainsAny"
                    or "ContainsAnyExcept")
                || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, extensions)
                || call.TargetMethod.Parameters.Length != 2
                || call.TargetMethod.Parameters[1].Type is not INamedTypeSymbol {
                    Name: "ReadOnlySpan",
                    TypeArguments.Length: 1
                } needles
                || needles.TypeArguments[0].SpecialType != SpecialType.System_Char
                || invocation.ArgumentList.Arguments[invocation.ArgumentList.Arguments.Count - 1] != argument
                || NullComparison.InsideExpressionTree(model, invocation, cancellation)) {
                return;
            }

            var replacement = invocation.ReplaceNode(
                argument.Expression,
                SyntaxFactory.ParseExpression("default(global::System.Buffers.SearchValues<char>)")
            );
            if (model.GetSpeculativeSymbolInfo(
                    invocation.SpanStart,
                    replacement,
                    SpeculativeBindingOption.BindAsExpression
                ).Symbol
                is not IMethodSymbol method
                || !SymbolEqualityComparer.Default.Equals(method.ContainingType, extensions)
                || method.Parameters.Length is not (1 or 2)
                || !SymbolEqualityComparer.Default.Equals(
                    method.Parameters[method.Parameters.Length - 1].Type,
                    searchType
                )) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Declaration.Variables[0].Identifier.GetLocation(),
                FixEdits.Pack(
                    (declaration.Declaration.Type.Span, "global::System.Buffers.SearchValues<char>"),
                    (initializer.Span, creation.ToString())
                ),
                "Precompute this constant character set with SearchValues"
            )
        );
    }

    static string? Characters(
        SemanticModel model,
        ExpressionSyntax initializer,
        System.Threading.CancellationToken cancellation
    ) {
        if (initializer is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
            && model.GetOperation(invocation, cancellation) is IInvocationOperation {
                TargetMethod:
                { Name: "ToCharArray", Parameters.Length: 0, ContainingType.SpecialType: SpecialType.System_String }
            }
            && model.GetConstantValue(access.Expression, cancellation) is { HasValue: true, Value: string text }) {
            return text;
        }

        IEnumerable<ExpressionSyntax>? expressions = initializer switch {
            ArrayCreationExpressionSyntax { Initializer: { } array } => array.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: { } array } => array.Expressions,
            CollectionExpressionSyntax collection when collection.Elements.All(static element => element
                is ExpressionElementSyntax
            )
                => collection.Elements.OfType<ExpressionElementSyntax>().Select(static element => element.Expression),
            _ => null
        };
        if (expressions is null) {
            return null;
        }

        var characters = new List<char>();
        foreach (var expression in expressions) {
            if (model.GetConstantValue(expression, cancellation) is not { HasValue: true, Value: char character }) {
                return null;
            }

            characters.Add(character);
        }

        return new(characters.ToArray());
    }
}
