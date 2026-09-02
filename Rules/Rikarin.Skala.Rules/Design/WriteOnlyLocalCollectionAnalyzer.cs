using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6062</c> — a local collection that is created, written to, and never read.
/// </summary>
/// <remarks>
///     <para>
///         Both halves look correct in isolation, which is why review does not catch it: the
///         declaration is ordinary, every <c>Add</c> is ordinary, and no line is wrong. What is
///         missing is a line. The compiler is satisfied — a collection that is written to is used, as
///         far as it is concerned — and every test passes, because the method's observable behaviour
///         is what it would be with the collection deleted.
///     </para>
///     <para>
///         ⚠ <b>Locals only, and that is what makes the question answerable at all.</b> A field or a
///         property can be read by any member of the type and by anything holding the instance, so
///         "nothing reads it" is the claim a single compilation cannot make — the assembly boundary
///         that closed #114 and #119, and the reason the <c>.Global</c> half of this concept is not
///         here. A local's every reference is inside one member.
///     </para>
///     <para>
///         ⚠ <b>Proved by exhaustion, not by dataflow</b> — the argument <c>SK2083</c> uses for the
///         mirror-image defect. Every reference must be the receiver of a mutating call whose result
///         is discarded, or the target of an indexer assignment. Anything else withdraws the finding
///         without the analyzer having to decide what it does.
///     </para>
///     <para>
///         ⚠ <b><c>SK2083</c> and this rule are disjoint by construction rather than by filter.</b>
///         <c>SK2083</c> requires a read and forbids every write; this requires a write and forbids
///         every read. No source satisfies both, so they can never report the same local.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WriteOnlyLocalCollectionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WriteOnlyLocalCollection);

    /// <summary>The collections whose members this rule is willing to classify.</summary>
    /// <remarks>
    ///     ⚠ A custom collection's <c>Add</c> may be the point of the call — an accumulator with an
    ///     observable side effect, a writer wearing a collection's clothes — and nothing here could
    ///     tell. The closed table is what makes "nothing reads it" mean the collection is dead rather
    ///     than that the analyzer could not see the reader.
    /// </remarks>
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

    /// <summary>The members that only ever put something in or take something out.</summary>
    static readonly string[] Mutators = [
        "Add",
        "AddFirst",
        "AddLast",
        "AddRange",
        "Clear",
        "Enqueue",
        "ExceptWith",
        "Insert",
        "InsertRange",
        "IntersectWith",
        "Push",
        "Remove",
        "RemoveAll",
        "RemoveAt",
        "RemoveRange",
        "Reverse",
        "Sort",
        "SymmetricExceptWith",
        "TrimExcess",
        "TryAdd",
        "UnionWith"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var creations = CollectionShape.Resolve(start.Compilation, Creations);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, creations),
                    SyntaxKind.LocalDeclarationStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> creations) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.Declaration.Variables.Count != 1) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer is not { Value: { } initializer }) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol local
            || !CollectionShape.Contains(creations, local.Type)) {
            return;
        }

        // The local has to be the collection, not a reference to somebody else's. `new List<T>()`
        // and a collection expression are the two spellings that certainly produce a fresh one; a
        // factory call, a cast or another local are all a handle on storage this member does not own.
        if (initializer is not (BaseObjectCreationExpressionSyntax or CollectionExpressionSyntax)) {
            return;
        }

        if (model.GetTypeInfo(initializer, cancellation).Type is not { TypeKind: not TypeKind.Error } created
            || !CollectionShape.Contains(creations, created)) {
            return;
        }

        // ⚠ The scan covers the whole enclosing member rather than running forward from the
        // declaration, so a lambda declared above the local — which cannot capture it — and one
        // declared below it — which holds a reference the scan sees — give the same answer. The
        // result not depending on statement order is what makes the exhaustion argument sound.
        var body = Enclosing(declarator);
        var written = false;

        foreach (var node in body.DescendantNodes()) {
            if (node is not IdentifierNameSyntax reference
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(reference, cancellation).Symbol,
                    local
                )) {
                continue;
            }

            if (!IsWrite(reference)) {
                return;
            }

            written = true;
        }

        if (!written) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declarator.Identifier.GetLocation(),
                "`"
                + local.Name
                + "` is filled and nothing in this member ever reads it, so the work that fills it "
                + "has no effect"
            )
        );
    }

    /// <summary>Whether this reference to the local only ever puts something into it.</summary>
    /// <remarks>
    ///     ⚠ A discarded return value is <em>required</em> rather than tolerated. <c>set.Remove(x)</c>
    ///     as a statement puts nothing anywhere the program can observe; <c>if (set.Remove(x))</c>
    ///     reads the collection through the <c>bool</c>, and that is a use. The same distinction is
    ///     what admits <c>Add</c> on a <c>HashSet</c>, whose result nobody looks at, and declines it
    ///     where somebody does.
    /// </remarks>
    static bool IsWrite(IdentifierNameSyntax reference) {
        switch (reference.Parent) {
            // `items[key] = value` — the collection is the target of an indexer assignment, and the
            // assignment is a statement so nothing reads the value back.
            case ElementAccessExpressionSyntax access when access.Expression == reference:
                return access.Parent is AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression
                } assignment
                    && assignment.Left == access
                    && assignment.Parent is ExpressionStatementSyntax;

            case MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } member when member.Expression == reference:
                return member.Parent is InvocationExpressionSyntax invocation
                    && invocation.Expression == member
                    && invocation.Parent is ExpressionStatementSyntax
                    && Array.IndexOf(Mutators, member.Name.Identifier.ValueText) >= 0;

            default:
                return false;
        }
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
