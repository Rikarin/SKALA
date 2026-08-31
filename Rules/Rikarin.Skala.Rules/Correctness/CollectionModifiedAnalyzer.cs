using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2007</c> — the <c>foreach</c> modifies the collection it is enumerating.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". The BCL collections carry a version
///     counter and <c>MoveNext</c> throws <c>InvalidOperationException</c> as soon as it moves. Because
///     the rule reports only loops with no way out — no <c>break</c>, no <c>return</c>, no
///     <c>throw</c>, no <c>goto</c> — the enumerator is certain to be advanced again after the
///     mutation, including past the last element where <c>MoveNext</c> still runs to return
///     <c>false</c>. This is a throw on every execution that reaches the mutation rather than a risk.
///     <para>
///         ⚠ The collection type is matched against a closed list, never against <c>ICollection&lt;T&gt;</c>.
///         A concurrent collection is designed to be written while it is read, and a custom implementation
///         may be; a rule that assumed otherwise would report the code that got it right.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionModifiedAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.CollectionModifiedDuringEnumeration);

    /// <summary>The BCL collections whose enumerator is version-checked.</summary>
    static readonly string[] VersionedTypes = [
        "System.Collections.Generic.List`1", "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1", "System.Collections.Generic.SortedList`2",
        "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.Queue`1", "System.Collections.Generic.Stack`1",
        "System.Collections.ObjectModel.Collection`1", "System.Collections.ObjectModel.ObservableCollection`1"
    ];

    /// <summary>The members that move the version counter.</summary>
    static readonly HashSet<string> Mutators = new(StringComparer.Ordinal) {
        "Add",
        "AddRange",
        "Remove",
        "RemoveAt",
        "RemoveAll",
        "RemoveRange",
        "Insert",
        "InsertRange",
        "Clear",
        "Enqueue",
        "Dequeue",
        "Push",
        "Pop",
        "TryAdd"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var versioned = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in VersionedTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        versioned.Add(type);
                    }
                }

                if (versioned.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, versioned), SyntaxKind.ForEachStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> versioned) {
        var loop = (ForEachStatementSyntax)context.Node;
        if (loop.Expression is not IdentifierNameSyntax and not MemberAccessExpressionSyntax) {
            return;
        }

        var type = context.SemanticModel.GetTypeInfo(loop.Expression, context.CancellationToken).Type;
        if (type is not INamedTypeSymbol named || !versioned.Contains(named.OriginalDefinition)) {
            return;
        }

        // ⚠ A local, a parameter or a field — never a property. A property that builds its list on
        // each read returns a different object to the `foreach` and to the `Remove`, so the two
        // would resolve to one symbol and be two collections.
        var source = context.SemanticModel.GetSymbolInfo(loop.Expression, context.CancellationToken).Symbol;
        if (source is not ILocalSymbol and not IParameterSymbol and not IFieldSymbol) {
            return;
        }

        // ⚠ Any way out of the loop and the argument stops holding: `items.Remove(item); break;` is
        // the one legal spelling of this, and the rule cannot tell which path a `return` was on.
        if (HasAnExit(loop.Statement)) {
            return;
        }

        var mutation = MutationOf(context, loop.Statement, source, loop.Expression);
        if (mutation is null) {
            return;
        }

        // ⚠ The fix is `.ToList()`, so `System.Linq` has to already be in scope. A repair whose text
        // does not compile is worse than no repair at all (docs/plan/10).
        if (!LinqIsInScope(context, loop)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                mutation.GetLocation(),
                FixEdits.Pack((new TextSpan(loop.Expression.Span.End, 0), ".ToList()")),
                "`" + source.Name + "` is modified while the `foreach` above is enumerating it"
            )
        );
    }

    /// <summary>The first mutating call on the enumerated collection, or null.</summary>
    static SyntaxNode? MutationOf(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        ISymbol source,
        ExpressionSyntax enumerated
    ) {
        var text = enumerated.ToString();
        foreach (var node in body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                         and not LocalFunctionStatementSyntax
                 )) {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
                || !Mutators.Contains(access.Name.Identifier.ValueText)) {
                continue;
            }

            // ⚠ Symbol *and* text. `a.Items` and `b.Items` are one symbol and two collections, and
            // the receiver is what tells them apart.
            var receiver = context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol;
            if (receiver is not null
                && SymbolEqualityComparer.Default.Equals(receiver, source)
                && string.Equals(access.Expression.ToString(), text, StringComparison.Ordinal)) {
                return invocation;
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether anything in the body could leave the loop before the next <c>MoveNext</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately blunt. A <c>break</c> inside a nested loop does not leave this one and a
    ///     <c>throw</c> inside a nested lambda does not either, but withholding the finding in those
    ///     cases costs a report; keeping it would cost a wrong one.
    /// </remarks>
    static bool HasAnExit(SyntaxNode body) {
        foreach (var node in body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                         and not LocalFunctionStatementSyntax
                 )) {
            switch (node) {
                case BreakStatementSyntax:
                case ReturnStatementSyntax:
                case ThrowStatementSyntax:
                case GotoStatementSyntax:
                case YieldStatementSyntax:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Whether <c>Enumerable</c> is reachable unqualified at this position.</summary>
    static bool LinqIsInScope(SyntaxNodeAnalysisContext context, SyntaxNode position) {
        foreach (var symbol in context.SemanticModel.LookupNamespacesAndTypes(position.SpanStart, name: "Enumerable")) {
            if (symbol is INamedTypeSymbol { ContainingNamespace: { } space }
                && string.Equals(space.ToDisplayString(), "System.Linq", StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }
}
