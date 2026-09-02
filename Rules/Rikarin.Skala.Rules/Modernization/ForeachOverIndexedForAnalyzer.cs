using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1083</c> — a <c>for</c> whose index only ever indexes one collection is a <c>foreach</c>.
/// </summary>
/// <remarks>
///     <para>
///         The index in this loop is not information: it is not printed, compared, passed anywhere or
///         used to reach a second collection. It exists to move a cursor the language already knows how
///         to move, and what the long form adds is three silent places to be off by one — the bound, the
///         comparison and the increment.
///     </para>
///     <para>
///         ⚠ <b>Three facts are proved before anything is reported.</b> <i>One</i>, the loop visits the
///         whole range in steps of one: a single <c>int i = 0</c>, a condition that is exactly
///         <c>i &lt; receiver.Count</c> or <c>i &lt; receiver.Length</c> against the receiver the body
///         indexes, and an incrementor that is exactly <c>i++</c> or <c>++i</c>. <i>Two</i>, the index is
///         used only as <c>receiver[i]</c> — any other appearance of it withdraws the finding, because
///         the element alone does not carry it. <i>Three</i>, the collection is not touched in the body
///         except through those reads.
///     </para>
///     <para>
///         ⚠ <b>The receiver must be an array, a <c>string</c>, a <c>List&lt;T&gt;</c> or an
///         <c>ImmutableList&lt;T&gt;</c></b> — types whose enumerator is documented to yield element
///         <c>0</c> through <c>Count - 1</c> in that order. An <c>IList&lt;T&gt;</c> or a hand-written
///         indexable type promises no such thing, so a <c>foreach</c> over one could visit a different
///         sequence entirely.
///     </para>
///     <para>
///         ⚠ <b>The residual risk, and the reason <c>fixIsSafe</c> is false, is mutation this cannot
///         see.</b> A <c>for</c> over a <c>List&lt;T&gt;</c> mutated through a method call in the body
///         keeps running where a <c>foreach</c> throws <c>InvalidOperationException</c>. Requiring the
///         receiver to be a local or a parameter shrinks that surface; proving no call reached the list
///         is not decidable and is not attempted. Arrays have no version field and are immune.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForeachOverIndexedForAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ForeachOverIndexedFor);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ForeachOverIndexedFor);

    static readonly string[] OrderedReceivers = [
        "System.Collections.Generic.List`1",
        "System.Collections.Immutable.ImmutableList`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var receivers = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var name in OrderedReceivers) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        receivers.Add(type);
                    }
                }

                var resolved = receivers.ToImmutable();
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, resolved),
                    SyntaxKind.ForStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> receivers) {
        var loop = (ForStatementSyntax)context.Node;

        if (loop.Declaration is not { Variables.Count: 1 } declaration
            || loop.Initializers.Count != 0
            || loop.Incrementors.Count != 1
            || declaration.Variables[0] is not { Initializer.Value: LiteralExpressionSyntax zero } declarator
            || !zero.IsKind(SyntaxKind.NumericLiteralExpression)
            || zero.Token.ValueText != "0") {
            return;
        }

        var index = declarator.Identifier.ValueText;
        if (!IsUnitStepOn(loop.Incrementors[0], index)) {
            return;
        }

        // ⚠ `<=` is not this shape. `i <= xs.Count` runs one element past the end and `i <= xs.Count - 1`
        // is the same range written so that a reader has to do the arithmetic; neither is a reading of
        // the header, and a rule that guesses between them is a rule that rewrites the wrong loop.
        if (loop.Condition is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LessThanExpression } bound
            || bound.Left is not IdentifierNameSyntax left
            || !string.Equals(left.Identifier.ValueText, index, StringComparison.Ordinal)
            || bound.Right is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } count
            || count.Name.Identifier.ValueText is not ("Count" or "Length")
            || !RewriteGuards.IsPlainNamePath(count.Expression)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol {
                Type.SpecialType: SpecialType.System_Int32
            } indexSymbol) {
            return;
        }

        // ⚠ A local or a parameter, never a field or a property. A property could hand back a
        // different collection on the second read, and a field is reachable by every call in the body.
        if (model.GetSymbolInfo(count.Expression, cancellation).Symbol is not { } receiverSymbol
            || receiverSymbol is not (ILocalSymbol or IParameterSymbol)) {
            return;
        }

        if (receiverSymbol is IParameterSymbol { RefKind: not RefKind.None }) {
            return;
        }

        var receiverType = model.GetTypeInfo(count.Expression, cancellation).Type;
        if (!IsOrderedIndexable(receiverType, receivers)) {
            return;
        }

        if (loop.Statement is not { } body || !Reads(body, model, indexSymbol, receiverSymbol, cancellation, out var reads)) {
            return;
        }

        // ⚠ A loop that never reads the collection is not this rule: the index is then a repeat count
        // and `foreach` would be saying something the original did not.
        if (reads.Count == 0) {
            return;
        }

        var receiverText = count.Expression.ToString();
        var name = ElementName(receiverText, model, loop, cancellation);
        if (name is null) {
            return;
        }

        // The whole header is replaced, so a comment or a directive inside it is content the fix
        // would delete.
        var header = TextSpan.FromBounds(loop.ForKeyword.SpanStart, loop.CloseParenToken.Span.End);
        if (RewriteGuards.ContainsCommentOrDirective(loop.SyntaxTree, header)) {
            return;
        }

        var edits = new List<(TextSpan, string)> {
            (header, "foreach (var " + name + " in " + receiverText + ")")
        };

        foreach (var read in reads) {
            edits.Add((read.Span, name));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(loop.SyntaxTree, header),
                FixEdits.Pack(edits.ToArray()),
                "`" + index + "` is only ever used to index `" + RewriteGuards.Trim(receiverText)
                + "`: `foreach (var " + name + " in " + RewriteGuards.Trim(receiverText) + ")`"
            )
        );
    }

    static bool IsUnitStepOn(ExpressionSyntax incrementor, string index) =>
        incrementor switch {
            PostfixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PostIncrementExpression, Operand: IdentifierNameSyntax post
                } =>
                string.Equals(post.Identifier.ValueText, index, StringComparison.Ordinal),
            PrefixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PreIncrementExpression, Operand: IdentifierNameSyntax pre
                } =>
                string.Equals(pre.Identifier.ValueText, index, StringComparison.Ordinal),
            _ => false
        };

    /// <summary>
    ///     Whether every use of the index and of the collection in the body is a read of
    ///     <c>receiver[index]</c>, collecting those reads.
    /// </summary>
    /// <remarks>
    ///     ⚠ The guard runs in both directions and both are load-bearing. An index used for anything
    ///     else — arithmetic, a second collection, an argument — is information the element does not
    ///     carry. A collection referenced for anything else is a collection the body may be mutating,
    ///     which is exactly the difference between a <c>for</c> that keeps running and a
    ///     <c>foreach</c> that throws. This is the same blunt guard <c>SK4006</c> uses.
    /// </remarks>
    static bool Reads(
        SyntaxNode body,
        SemanticModel model,
        ILocalSymbol index,
        ISymbol receiver,
        System.Threading.CancellationToken cancellation,
        out List<ElementAccessExpressionSyntax> reads
    ) {
        reads = [];
        foreach (var node in body.DescendantNodes()) {
            if (node is not IdentifierNameSyntax identifier) {
                continue;
            }

            var symbol = model.GetSymbolInfo(identifier, cancellation).Symbol;
            var isIndex = SymbolEqualityComparer.Default.Equals(symbol, index);
            var isReceiver = SymbolEqualityComparer.Default.Equals(symbol, receiver);
            if (!isIndex && !isReceiver) {
                continue;
            }

            if (isIndex) {
                // The index must be the sole argument of an element access over the receiver.
                if (identifier.Parent is not ArgumentSyntax argument
                    || argument.Parent is not BracketedArgumentListSyntax { Arguments.Count: 1 } list
                    || list.Parent is not ElementAccessExpressionSyntax access
                    || !IsReadOf(access, model, receiver, cancellation)) {
                    return false;
                }

                if (!reads.Contains(access)) {
                    reads.Add(access);
                }

                continue;
            }

            // The receiver may appear only as the thing being indexed by one of those reads.
            if (identifier.Parent is not ElementAccessExpressionSyntax indexed
                || indexed.Expression != identifier
                || !IsReadOf(indexed, model, receiver, cancellation)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether an element access is a plain read of the receiver — not written to, not passed by
    ///     reference, not incremented.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>xs[i] = v</c> and <c>xs[i]++</c> mutate the collection, and no <c>foreach</c> variable
    ///     can stand in for the target of an assignment: the iteration variable is a copy and writing
    ///     to it would silently stop updating anything.
    /// </remarks>
    static bool IsReadOf(
        ElementAccessExpressionSyntax access,
        SemanticModel model,
        ISymbol receiver,
        System.Threading.CancellationToken cancellation
    ) {
        if (access.Expression is not IdentifierNameSyntax name
            || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, receiver)) {
            return false;
        }

        switch (access.Parent) {
            case AssignmentExpressionSyntax assignment when assignment.Left == access:
            case PostfixUnaryExpressionSyntax:
            case PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
            }:
                return false;

            case ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None }:
                return false;

            default:
                return true;
        }
    }

    static bool IsOrderedIndexable(ITypeSymbol? type, ImmutableArray<INamedTypeSymbol> receivers) {
        switch (type) {
            case IArrayTypeSymbol { IsSZArray: true }:
                return true;

            case INamedTypeSymbol { SpecialType: SpecialType.System_String }:
                return true;

            case INamedTypeSymbol named:
                foreach (var candidate in receivers) {
                    if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, candidate)) {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    /// <summary>
    ///     A name for the element that nothing in the member already uses, derived from the collection's.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both halves of the scoping guard are asked, for the reason <c>RewriteGuards</c> documents:
    ///     a lookup answers what is in scope at the loop and a member scan answers what a neighbouring
    ///     scope declares, and <c>CS0136</c> is about the second.
    /// </remarks>
    static string? ElementName(
        string receiver,
        SemanticModel model,
        ForStatementSyntax loop,
        System.Threading.CancellationToken cancellation
    ) {
        var last = receiver;
        var dot = last.LastIndexOf('.');
        if (dot >= 0) {
            last = last.Substring(dot + 1);
        }

        var candidates = new List<string>();
        if (last.Length > 1 && last.EndsWith("s", StringComparison.Ordinal)) {
            var singular = last.Substring(0, last.Length - 1);
            if (singular.EndsWith("ie", StringComparison.Ordinal)) {
                candidates.Add(singular.Substring(0, singular.Length - 2) + "y");
            }

            candidates.Add(singular);
        }

        candidates.Add("item");
        candidates.Add("element");
        candidates.Add("current");

        foreach (var candidate in candidates) {
            if (candidate.Length == 0 || !SyntaxFacts.IsValidIdentifier(candidate)
                || SyntaxFacts.GetKeywordKind(candidate) != SyntaxKind.None) {
                continue;
            }

            if (RewriteGuards.WouldCollide(model, loop.SpanStart, candidate, cancellation)
                || RewriteGuards.DeclaredElsewhereInMember(loop, candidate)) {
                continue;
            }

            return candidate;
        }

        return null;
    }
}
