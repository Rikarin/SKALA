using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3043</c> — two paths in one type take the same pair of locks in opposite orders.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     The textbook deadlock, and the reason it survives review is that the two halves are almost
///     never on the same screen: each method reads as a perfectly ordinary pair of nested locks, and
///     only the two together are wrong. Nothing in Roslyn or the <c>CA*</c> set reports it.
///     <para>
///         The analysis is a lock <em>order</em> and nothing more. Every <c>lock</c> whose target
///         resolves to a field of the type under analysis contributes an ordered pair for each lock it
///         is nested inside, and a pair present in both directions is the finding. It never asks
///         whether the two paths run concurrently, because the pair is a claim about the type's
///         lock hierarchy and a type with no hierarchy is what the rule is asking for.
///     </para>
///     <para>
///         ⚠ The unit of analysis is the <em>symbol</em>, not the file. Issue #56's whole argument is
///         that the two halves are rarely in the same file, so a rule that only saw one partial
///         declaration would miss exactly the case worth reporting. That is why this rule is
///         <c>scope: Compilation</c> and excluded from per-file caching: its answer for one file
///         depends on files the cache key does not name.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LockOrderAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InconsistentLockOrder);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(
            static start => {
                if (start.Symbol is not INamedTypeSymbol owner) {
                    return;
                }

                // ⚠ The syntax node actions below run concurrently across the type's declarations,
                // so the accumulator has to be one. Keyed on the field *names*, which is exact:
                // every key admitted by `Key` is a field of `owner`, and a type cannot declare two
                // fields with the same name.
                var pairs = new ConcurrentDictionary<(string Outer, string Inner), Location>();
                start.RegisterSyntaxNodeAction(
                    context => Collect(context, owner, pairs),
                    SyntaxKind.LockStatement
                );
                start.RegisterSymbolEndAction(context => Report(context, pairs));
            },
            SymbolKind.NamedType
        );
    }

    static void Collect(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol owner,
        ConcurrentDictionary<(string, string), Location> pairs
    ) {
        // ⚠ There is deliberately no "is this node really in `owner`" guard here, and the first
        // draft had one. A nested type's declarations sit inside the outer type's, so the obvious
        // worry is that the same `lock` reaches both symbols' actions and the outer type inherits
        // the inner type's hierarchy. It does not: Roslyn scopes a symbol-start syntax action to
        // the symbol's own members. That was measured rather than assumed — with the guard deleted,
        // `SK3043/negative/a-nested-type-with-the-same-field-names.cs` stays green, which is what
        // made the guard dead code no sabotage could kill. The fixture stays as the pin on that
        // behaviour; if Roslyn ever changes it, the fixture goes red and the guard comes back with
        // a test that proves it.
        var statement = (LockStatementSyntax)context.Node;
        if (Key(statement.Expression, context.SemanticModel, owner, context.CancellationToken) is not { } inner) {
            return;
        }

        foreach (var enclosing in Enclosing(statement)) {
            if (Key(enclosing.Expression, context.SemanticModel, owner, context.CancellationToken) is not { } outer
                || string.Equals(outer, inner, StringComparison.Ordinal)) {
                // ⚠ A re-entrant `lock (a) { lock (a) { … } }` is not an order and never deadlocks:
                // a monitor is recursive for the thread that holds it.
                continue;
            }

            pairs.TryAdd((outer, inner), statement.Expression.GetLocation());
        }
    }

    /// <summary>
    ///     The <c>lock</c> statements this one is nested inside, innermost first.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk stops at a lambda or a local function. A delegate <em>written</em> inside a
    ///     <c>lock</c> body does not run inside it — it runs whenever somebody invokes it, on whatever
    ///     thread they are on and holding whatever they happen to hold — so treating the enclosing
    ///     lock as held would invent an ordering the program does not have. That is the one direction
    ///     this rule must never guess in, because the invented pair is what produces the finding.
    /// </remarks>
    static IEnumerable<LockStatementSyntax> Enclosing(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case LockStatementSyntax found:
                    yield return found;

                    break;
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case MemberDeclarationSyntax:
                    yield break;
            }
        }
    }

    /// <summary>
    ///     The name of the field a <c>lock</c> target is, or <c>null</c> where the target is anything
    ///     else.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two spellings only: a bare identifier and <c>this.field</c>. Everything else — a local, a
    ///     parameter, <c>peer.gate</c> — is opaque, and that is a <em>recall</em> decision rather than a
    ///     soundness one, stated here because it is easy to mistake for the other. A cycle over two
    ///     fields reached through two different instances is a real deadlock, the classic
    ///     bank-transfer one, and this rule does not report it: the message it would produce names
    ///     fields and not objects, so "`a` while holding `b`" would be an ambiguous sentence about
    ///     which object each name meant. Widening the rule needs an answer to that, and the negative
    ///     fixture set records the miss rather than hiding it.
    ///     <para>
    ///         An opaque target is not fatal to the locks around it. It contributes no pair of its own
    ///         and does not break the pairs recorded across it, because
    ///         <c>lock (a) { lock (opaque) { lock (b) } }</c> still takes <c>a</c> before <c>b</c>.
    ///     </para>
    /// </remarks>
    static string? Key(
        ExpressionSyntax expression,
        SemanticModel model,
        INamedTypeSymbol owner,
        CancellationToken cancellation
    ) {
        var named = expression switch {
            IdentifierNameSyntax => expression,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: { } name } => name,
            _ => null
        };

        return named is not null
            && model.GetSymbolInfo(named, cancellation).Symbol is IFieldSymbol field
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, owner)
                ? field.Name
                : null;
    }

    static void Report(
        SymbolAnalysisContext context,
        ConcurrentDictionary<(string Outer, string Inner), Location> pairs
    ) {
        // ⚠ Ordered before reporting. A `ConcurrentDictionary` enumerates in whatever order the
        // analysis happened to fill it, and a diagnostic whose location depends on thread timing is
        // a baseline that churns for no reason.
        var ordered = pairs
            .OrderBy(static entry => entry.Key.Outer, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.Inner, StringComparer.Ordinal)
            .ToArray();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in ordered) {
            var key = entry.Key;
            var location = entry.Value;
            if (!pairs.TryGetValue((key.Inner, key.Outer), out var opposite)) {
                continue;
            }

            // One finding per unordered pair. The two directions are one deadlock.
            if (!seen.Add(
                    string.CompareOrdinal(key.Outer, key.Inner) < 0
                        ? key.Outer + " " + key.Inner
                        : key.Inner + " " + key.Outer
                )) {
                continue;
            }

            var other = opposite.GetLineSpan();
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    location,
                    "`"
                    + key.Inner
                    + "` is taken while holding `"
                    + key.Outer
                    + "` here, and `"
                    + key.Outer
                    + "` while holding `"
                    + key.Inner
                    + "` at "
                    + System.IO.Path.GetFileName(other.Path)
                    + "("
                    + (other.StartLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + (other.StartLinePosition.Character + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + "); two threads on those paths deadlock"
                )
            );
        }
    }
}
