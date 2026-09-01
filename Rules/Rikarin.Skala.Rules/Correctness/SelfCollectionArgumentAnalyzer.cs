using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2081</c> — a collection handed to one of its own members as the other collection.
/// </summary>
/// <remarks>
///     <para>
///         <c>set.UnionWith(set)</c> does nothing, <c>set.ExceptWith(set)</c> is <c>Clear()</c> written
///         so that nobody reads it as <c>Clear()</c>, <c>set.SetEquals(set)</c> is <c>true</c> with a
///         hash walk in front of it, and <c>list.AddRange(list)</c> doubles the list in place. None of
///         them is what anybody sits down to write. The defect underneath is almost always one
///         identifier: the second collection was meant to be a different one.
///     </para>
///     <para>
///         ⚠ <b>The two sides have to be the same <em>storage</em>, which is a symbol comparison and
///         not a text one.</b> Two spellings that read alike — <c>a.items</c> and <c>b.items</c> —
///         resolve to one field symbol through two different receivers, so the walk compares the
///         receivers too and stops at the first symbol that differs. And every symbol along the path
///         must be a local, a parameter or a field: a property is an accessor call, and two reads of
///         one property may hand back two different objects.
///     </para>
///     <para>
///         ⚠ <c>a.Equals(a)</c> is deliberately outside the table even though it is the shape the
///         upstream rule's title suggests. Asserting that a type's equality is reflexive is what an
///         equality test is for, and this repository ships a rule about equality contracts —
///         <c>SK2004</c> — whose own fixtures would be the first false positives.
///     </para>
///     <para>
///         ⚠ <c>Concat</c>, <c>Zip</c> and <c>Array.Copy</c> are outside it for the opposite reason.
///         <c>items.Concat(items)</c> is a legitimate way to say "twice", and
///         <c>Array.Copy(buffer, 1, buffer, 0, n)</c> is how a shift is written. A degenerate result is
///         the entry condition for this table, not an unusual-looking argument.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelfCollectionArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CollectionPassedToItself);

    /// <summary>
    ///     The member, and what it degenerates to when both collections are the same object.
    /// </summary>
    /// <remarks>
    ///     <c>Index</c> counts the parameters that are not the receiver, so it is the argument index
    ///     for an instance member and the index after the source for an <c>Enumerable</c> extension.
    /// </remarks>
    static readonly (string Type, string Method, int Index, string Consequence)[] Table = [
        ("System.Collections.Generic.HashSet`1", "UnionWith", 0, "does nothing"),
        ("System.Collections.Generic.HashSet`1", "IntersectWith", 0, "does nothing"),
        ("System.Collections.Generic.HashSet`1", "ExceptWith", 0, "empties the set, which `Clear()` says"),
        (
            "System.Collections.Generic.HashSet`1", "SymmetricExceptWith", 0,
            "empties the set, which `Clear()` says"
        ),
        ("System.Collections.Generic.HashSet`1", "IsSubsetOf", 0, "is always true"),
        ("System.Collections.Generic.HashSet`1", "IsSupersetOf", 0, "is always true"),
        ("System.Collections.Generic.HashSet`1", "IsProperSubsetOf", 0, "is always false"),
        ("System.Collections.Generic.HashSet`1", "IsProperSupersetOf", 0, "is always false"),
        ("System.Collections.Generic.HashSet`1", "SetEquals", 0, "is always true"),
        ("System.Collections.Generic.HashSet`1", "Overlaps", 0, "is true unless the set is empty"),
        ("System.Collections.Generic.SortedSet`1", "UnionWith", 0, "does nothing"),
        ("System.Collections.Generic.SortedSet`1", "IntersectWith", 0, "does nothing"),
        ("System.Collections.Generic.SortedSet`1", "ExceptWith", 0, "empties the set, which `Clear()` says"),
        (
            "System.Collections.Generic.SortedSet`1", "SymmetricExceptWith", 0,
            "empties the set, which `Clear()` says"
        ),
        ("System.Collections.Generic.SortedSet`1", "IsSubsetOf", 0, "is always true"),
        ("System.Collections.Generic.SortedSet`1", "IsSupersetOf", 0, "is always true"),
        ("System.Collections.Generic.SortedSet`1", "IsProperSubsetOf", 0, "is always false"),
        ("System.Collections.Generic.SortedSet`1", "IsProperSupersetOf", 0, "is always false"),
        ("System.Collections.Generic.SortedSet`1", "SetEquals", 0, "is always true"),
        ("System.Collections.Generic.SortedSet`1", "Overlaps", 0, "is true unless the set is empty"),
        ("System.Collections.Generic.List`1", "AddRange", 0, "appends the list to itself and doubles it"),
        ("System.Collections.Generic.List`1", "InsertRange", 1, "splices the list into itself and doubles it"),
        ("System.Array", "CopyTo", 0, "copies the array over itself"),
        ("System.Linq.Enumerable", "SequenceEqual", 0, "is always true"),
        ("System.Linq.Enumerable", "Except", 0, "is always empty"),
        ("System.Linq.Enumerable", "Intersect", 0, "is `Distinct()`"),
        ("System.Linq.Enumerable", "Union", 0, "is `Distinct()`")
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var entries = new List<Entry>();
                foreach (var (type, method, index, consequence) in Table) {
                    if (start.Compilation.GetTypeByMetadataName(type) is { } symbol) {
                        entries.Add(new Entry(symbol, method, index, consequence));
                    }
                }

                if (entries.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, entries),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    sealed record Entry(INamedTypeSymbol Type, string Method, int Index, string Consequence);

    static void Analyze(SyntaxNodeAnalysisContext context, List<Entry> entries) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        var entry = Find(entries, definition);
        if (entry is null) {
            return;
        }

        // ⚠ The receiver is one of two different things. An extension called in reduced form puts
        // the source before the dot and every declared parameter after it; the same extension
        // called statically puts the source in the argument list, one slot ahead of everything the
        // table counts.
        var isExtension = definition.IsStatic && definition.IsExtensionMethod;
        var arguments = invocation.ArgumentList.Arguments;

        ExpressionSyntax source;
        int otherIndex;
        if (isExtension && method.ReducedFrom is null) {
            if (arguments.Count <= entry.Index + 1) {
                return;
            }

            source = arguments[0].Expression;
            otherIndex = entry.Index + 1;
        } else {
            if (invocation.Expression is not MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
                } access
                || arguments.Count <= entry.Index) {
                return;
            }

            source = access.Expression;
            otherIndex = entry.Index;
        }

        var other = arguments[otherIndex];

        // A named argument may not be in the slot it is written in, and a `ref` argument is a
        // different question about the same syntax.
        foreach (var argument in arguments) {
            if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
                return;
            }
        }

        if (!CollectionShape.SameStorage(model, source, other.Expression, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                other.Expression.GetLocation(),
                "`"
                + source
                + "` is the collection this call is made on, so `"
                + entry.Type.Name
                + "."
                + entry.Method
                + "` "
                + entry.Consequence
            )
        );
    }

    static Entry? Find(List<Entry> entries, IMethodSymbol definition) {
        foreach (var entry in entries) {
            if (string.Equals(definition.Name, entry.Method, System.StringComparison.Ordinal)
                && SymbolEqualityComparer.Default.Equals(definition.ContainingType.OriginalDefinition, entry.Type)) {
                return entry;
            }
        }

        return null;
    }
}
