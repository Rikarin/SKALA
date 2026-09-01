using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6031</c> — a non-private <c>readonly</c> field holding an array or a mutable collection.
/// </summary>
/// <remarks>
///     <c>readonly</c> constrains the field, never the object in it. On a <c>public string[] Names</c>
///     it stops <c>Names = …</c> and stops nothing else: every caller can still write
///     <c>Names[0] = "…"</c>, and on a <c>List&lt;T&gt;</c> can clear it. The modifier reads as a
///     guarantee and provides none, which is worse than no modifier at all — a reader who sees it stops
///     looking for the mutation.
///     <para>
///         ⚠ <b>The mutable types are an explicit list, and an interface test would have been wrong.</b>
///         <c>ImmutableArray&lt;T&gt;</c>, <c>ImmutableList&lt;T&gt;</c> and
///         <c>ReadOnlyCollection&lt;T&gt;</c> all implement <c>IList&lt;T&gt;</c> — explicitly, throwing
///         from every mutator — so "does the type implement <c>ICollection&lt;T&gt;</c>" reports the
///         immutable collections the fix would tell somebody to switch to. The cost of the list is a
///         miss on a user-defined mutable type, which is the right direction for a rule whose whole risk
///         is over-firing.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadonlyMutableFieldAnalyzer : DiagnosticAnalyzer {
    /// <summary>
    ///     The types whose contents any holder can change. ⚠ Concurrent collections are deliberately
    ///     absent — see the rule's <c>falsePositives</c>.
    /// </summary>
    static readonly string[] MutableTypes = [
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.LinkedList`1",
        "System.Collections.Generic.ICollection`1",
        "System.Collections.Generic.IList`1",
        "System.Collections.Generic.IDictionary`2",
        "System.Collections.Generic.ISet`1",
        "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.ObservableCollection`1",
        "System.Collections.ArrayList",
        "System.Collections.Hashtable",
        "System.Collections.SortedList",
        "System.Collections.Queue",
        "System.Collections.Stack",
        "System.Collections.IList",
        "System.Collections.IDictionary",
        "System.Collections.ICollection"
    ];

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ReadonlyMutableField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var mutable = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in MutableTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        mutable.Add(type);
                    }
                }

                start.RegisterSyntaxNodeAction(node => Analyze(node, mutable), SyntaxKind.FieldDeclaration);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> mutable) {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!IsReadonlyAndEscapesTheType(field.Modifiers)) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type;
        if (type is null || type.TypeKind == TypeKind.Error) {
            // ⚠ An unresolved type answers "not a mutable collection" for the wrong reason. Silence
            // is the only honest result, and it is why the rule declares `scope: Semantic`.
            return;
        }

        var array = type.TypeKind == TypeKind.Array;
        if (!array && (type is not INamedTypeSymbol named || !mutable.Contains(named.OriginalDefinition))) {
            return;
        }

        foreach (var declarator in field.Declaration.Variables) {
            // A zero-length array has no element anybody could write, so `readonly` on one really is
            // the whole guarantee. `Array.Empty<T>()`, `[]` and `new T[0]` are the three ways to say
            // it, and all three are visible in the initializer.
            if (array && IsProvablyEmptyArray(declarator.Initializer?.Value)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    declarator.Identifier.GetLocation(),
                    "`"
                    + declarator.Identifier.ValueText
                    + "` is `readonly` and its contents are not: every caller that can see the field "
                    + "can still write through it, so the modifier promises an immutability the field "
                    + "does not have"
                )
            );
        }
    }

    /// <summary>
    ///     <c>readonly</c>, and reachable from outside the declaring type.
    /// </summary>
    /// <remarks>
    ///     ⚠ A private field is never reported however mutable it is. The type owns its own state, and
    ///     <c>readonly</c> there is a note to the type's own author rather than a promise made to
    ///     anybody. Anything that is not plain <c>private</c> — including <c>internal</c> and
    ///     <c>private protected</c> — hands the object to code the declaration cannot see.
    /// </remarks>
    static bool IsReadonlyAndEscapesTheType(SyntaxTokenList modifiers) {
        var isReadonly = false;
        var escapes = false;

        foreach (var modifier in modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                case SyntaxKind.ReadOnlyKeyword:
                    isReadonly = true;
                    break;

                // ⚠ `private protected` carries `private` *and* `protected`, and it still hands the
                // object to a derived type. Testing for the presence of a widening keyword rather than
                // for the absence of `private` is what gets that combination right; a field with no
                // accessibility modifier at all is private and falls out here with `escapes` false.
                case SyntaxKind.PublicKeyword:
                case SyntaxKind.ProtectedKeyword:
                case SyntaxKind.InternalKeyword:
                    escapes = true;
                    break;
            }
        }

        return isReadonly && escapes;
    }

    static bool IsProvablyEmptyArray(ExpressionSyntax? value) =>
        value switch {
            CollectionExpressionSyntax { Elements.Count: 0 } => true,
            InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Empty" } }
            } => true,
            ArrayCreationExpressionSyntax creation => IsZeroLength(creation),
            _ => false
        };

    static bool IsZeroLength(ArrayCreationExpressionSyntax creation) {
        if (creation.Type.RankSpecifiers.Count != 1 || creation.Type.RankSpecifiers[0].Sizes.Count != 1) {
            return false;
        }

        return creation.Type.RankSpecifiers[0].Sizes[0] is LiteralExpressionSyntax { Token.ValueText: "0" };
    }
}
