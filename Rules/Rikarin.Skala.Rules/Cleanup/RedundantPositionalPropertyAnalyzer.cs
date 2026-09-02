using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0282</c> — a record property that restates the one its parameter already generates.</summary>
/// <remarks>
///     <para>
///         <c>record R(int X) { public int X { get; init; } = X; }</c> writes out, character for
///         character, the property the positional parameter generates. It is a shape a model reaches
///         for constantly, because "declare the properties" is what most of its training data does and
///         the positional form is newer than most of it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Which accessor pair is generated depends on the record kind, and getting that wrong
///             either misses every finding or deletes a property that was doing something.
///         </b> A
///         <c>record class</c> and a <c>readonly record struct</c> generate <c>{ get; init; }</c>; a
///         mutable <c>record struct</c> generates <c>{ get; set; }</c>. So <c>{ get; init; }</c> written
///         on a mutable record struct is <em>not</em> redundant — it makes the property init-only,
///         which the generated one is not — and it is declined.
///     </para>
///     <para>
///         ⚠ <b>The initializer must be the parameter itself, and it is asked of the symbol.</b>
///         <c>= X</c> where <c>X</c> binds to anything but the primary constructor's parameter is a
///         different program. Without an initializer at all the declaration is not redundant either: a
///         hand-written positional property is not assigned by the generated constructor, so deleting
///         it would change what the record stores.
///     </para>
///     <para>
///         ⚠ <b>An attribute or an accessor modifier withdraws the finding.</b>
///         <c>[JsonPropertyName("x")] public int X { get; init; } = X;</c> is not the generated
///         property, and neither is <c>{ get; private init; }</c>. The generated one carries no
///         attribute and no accessor accessibility, so anything written there is the author's and the
///         deletion would take it.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantPositionalPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantPositionalProperty);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.Parent is not RecordDeclarationSyntax { ParameterList: { } parameters } record
            || property.AttributeLists.Count > 0
            || property.Initializer is not { Value: IdentifierNameSyntax initializer }
            || property.ExpressionBody is not null) {
            return;
        }

        var name = property.Identifier.ValueText;
        var parameter = Find(parameters, name);
        if (parameter is null
            || !SyntaxFactory.AreEquivalent(parameter.Type, property.Type, false)
            || parameter.Modifiers.Count > 0
            || parameter.AttributeLists.Count > 0
            || parameter.Default is not null) {
            return;
        }

        if (!IsExactlyPublic(property.Modifiers)
            || !HasGeneratedAccessors(property, record)
            || !string.Equals(initializer.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
            return;
        }

        // ⚠ The written `X` has to be the primary constructor's parameter and not some other `X` the
        // scope happens to hold; the two are indistinguishable in the syntax.
        if (context.SemanticModel.GetSymbolInfo(initializer, context.CancellationToken).Symbol
            is not IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } }) {
            return;
        }

        // The whole declaration goes, leading trivia included, so a documentation comment on it is a
        // reason to leave it alone rather than a thing to delete.
        if (RewriteGuards.ContainsCommentOrDirectiveAroundTheDeclaration(property)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                property.Identifier.GetLocation(),
                FixEdits.Pack((property.FullSpan, string.Empty)),
                $"the positional parameter `{name}` generates exactly this property, initializer included"
            )
        );
    }

    static ParameterSyntax? Find(ParameterListSyntax parameters, string name) {
        foreach (var parameter in parameters.Parameters) {
            if (string.Equals(parameter.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>The generated property is <c>public</c> and nothing else.</summary>
    static bool IsExactlyPublic(SyntaxTokenList modifiers) =>
        modifiers.Count == 1 && modifiers[0].IsKind(SyntaxKind.PublicKeyword);

    /// <summary>
    ///     ⚠ <c>{ get; init; }</c> everywhere except a mutable <c>record struct</c>, which generates
    ///     <c>{ get; set; }</c>.
    /// </summary>
    static bool HasGeneratedAccessors(PropertyDeclarationSyntax property, RecordDeclarationSyntax record) {
        if (property.AccessorList is not { Accessors.Count: 2 } list) {
            return false;
        }

        var mutableStruct = record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            && !HasReadOnly(record.Modifiers);

        var expected = mutableStruct ? SyntaxKind.SetAccessorDeclaration : SyntaxKind.InitAccessorDeclaration;

        return IsPlain(list.Accessors[0], SyntaxKind.GetAccessorDeclaration) && IsPlain(list.Accessors[1], expected);
    }

    static bool HasReadOnly(SyntaxTokenList modifiers) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(SyntaxKind.ReadOnlyKeyword)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>An accessor with no body, no expression body, no modifier and no attribute.</summary>
    static bool IsPlain(AccessorDeclarationSyntax accessor, SyntaxKind kind) =>
        accessor.IsKind(kind)
        && accessor.Body is null
        && accessor.ExpressionBody is null
        && accessor.Modifiers.Count == 0
        && accessor.AttributeLists.Count == 0;
}
