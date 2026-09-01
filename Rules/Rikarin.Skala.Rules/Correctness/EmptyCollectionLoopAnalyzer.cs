using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2083</c> — a <c>foreach</c> over a local that was created empty and is never touched
///     again, so the body cannot run.
/// </summary>
/// <remarks>
///     <para>
///         The population step was removed, or was never written. The loop reads as working code, the
///         compiler has nothing to say about it, and every test over it passes.
///     </para>
///     <para>
///         ⚠ <b>This is proved by exhaustion, not by dataflow.</b> Roslyn's <c>AnalyzeDataFlow</c>
///         does not answer questions about a collection's contents, so the rule asks a question it
///         can answer instead: <em>every</em> reference to the local inside the member that declares
///         it must be the subject of a <c>foreach</c>. A collection can only be filled through a
///         member call, an assignment, or by being handed somewhere, and each of those is a reference
///         that is not a <c>foreach</c> subject. One reference of any other kind — an argument, a
///         receiver, an assignment target, a <c>ref</c>, a capture — withdraws the finding, without
///         the analyzer having to decide what that reference does.
///     </para>
///     <para>
///         ⚠ The scan runs over the whole enclosing member, not from the declaration forward. A local
///         function or a lambda declared *before* the local cannot capture it, and one declared after
///         it holds a reference the scan sees; either way the answer does not depend on ordering,
///         which is what makes the exhaustion argument sound.
///     </para>
///     <para>
///         ⚠ Locals only. A field or a property can be filled by any member of the type and by
///         anything holding the instance, so "nothing adds to it" is not a claim available there.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCollectionLoopAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ProvablyEmptyCollection);

    /// <summary>
    ///     The collections whose parameterless constructor is documented to produce an empty one.
    /// </summary>
    static readonly string[] Creations = [
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.LinkedList`1",
        "System.Collections.ObjectModel.Collection`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var creations = new List<INamedTypeSymbol>();
                foreach (var name in Creations) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        creations.Add(type);
                    }
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, creations),
                    SyntaxKind.ForEachStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> creations) {
        var loop = (ForEachStatementSyntax)context.Node;
        if (loop.Expression is not IdentifierNameSyntax name) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(name, cancellation).Symbol is not ILocalSymbol local
            || local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not VariableDeclaratorSyntax {
                Initializer.Value: { } initializer
            } declarator) {
            return;
        }

        if (!IsEmptyCreation(model, initializer, creations, cancellation)) {
            return;
        }

        var body = Enclosing(declarator);
        foreach (var node in body.DescendantNodes()) {
            if (node is not IdentifierNameSyntax reference
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(reference, cancellation).Symbol,
                    local
                )) {
                continue;
            }

            // The only reference a collection survives untouched is being the subject of a loop.
            if (reference.Parent is not ForEachStatementSyntax subject || subject.Expression != reference) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                loop.Expression.GetLocation(),
                "`"
                + local.Name
                + "` is created empty and nothing in this member ever adds to it, so the loop body "
                + "never runs"
            )
        );
    }

    /// <summary>Whether the initialiser certainly produces a collection with no elements.</summary>
    /// <remarks>
    ///     ⚠ Any constructor argument withdraws it. A capacity leaves the collection empty and a
    ///     source collection does not, and telling the two apart is a question the rule does not need
    ///     to ask: an initialiser with an argument is rare enough that declining it costs nothing.
    /// </remarks>
    static bool IsEmptyCreation(
        SemanticModel model,
        ExpressionSyntax initializer,
        List<INamedTypeSymbol> creations,
        CancellationToken cancellation
    ) {
        switch (initializer) {
            case CollectionExpressionSyntax { Elements.Count: 0 }:
                return true;

            case ArrayCreationExpressionSyntax array:
                return IsEmptyArray(model, array, cancellation);

            case ImplicitArrayCreationExpressionSyntax { Initializer.Expressions.Count: 0 }:
                return true;

            case InvocationExpressionSyntax invocation:
                return IsEmptyFactory(model, invocation, cancellation);

            case BaseObjectCreationExpressionSyntax creation:
                return creation.ArgumentList is null or { Arguments.Count: 0 }
                    && creation.Initializer is null or { Expressions.Count: 0 }
                    && IsKnownCollection(model, creation, creations, cancellation);

            default:
                return false;
        }
    }

    static bool IsEmptyArray(
        SemanticModel model,
        ArrayCreationExpressionSyntax array,
        CancellationToken cancellation
    ) {
        if (array.Initializer is { }) {
            return array.Initializer.Expressions.Count == 0;
        }

        if (array.Type.RankSpecifiers.Count != 1 || array.Type.RankSpecifiers[0].Sizes.Count != 1) {
            return false;
        }

        var size = model.GetConstantValue(array.Type.RankSpecifiers[0].Sizes[0], cancellation);
        return size is { HasValue: true, Value: 0 };
    }

    /// <summary><c>Array.Empty&lt;T&gt;()</c> and <c>Enumerable.Empty&lt;T&gt;()</c>.</summary>
    static bool IsEmptyFactory(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellation
    ) {
        if (invocation.ArgumentList.Arguments.Count != 0
            || model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol {
                IsStatic: true, Name: "Empty"
            } method) {
            return false;
        }

        var container = method.ContainingType.ToDisplayString();
        return string.Equals(container, "System.Array", StringComparison.Ordinal)
            || string.Equals(container, "System.Linq.Enumerable", StringComparison.Ordinal);
    }

    static bool IsKnownCollection(
        SemanticModel model,
        BaseObjectCreationExpressionSyntax creation,
        List<INamedTypeSymbol> creations,
        CancellationToken cancellation
    ) {
        if (model.GetTypeInfo(creation, cancellation).Type is not INamedTypeSymbol type
            || type.TypeKind == TypeKind.Error) {
            return false;
        }

        foreach (var candidate in creations) {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, candidate)) {
                return true;
            }
        }

        return false;
    }

    static SyntaxNode Enclosing(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            if (current is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or CompilationUnitSyntax) {
                return current;
            }
        }

        return node;
    }
}
