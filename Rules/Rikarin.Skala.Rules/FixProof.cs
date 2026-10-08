using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>
///     ⚠ The re-parse half of proving a fix: the text a fix writes must parse back to the tree the fix
///     meant to build, everywhere and not only inside the edit (#424).
/// </summary>
/// <remarks>
///     ⚠ A fix is a text edit, and the parser reads the text the edit leaves behind with no memory of
///     the tree it came from. Deleting one token can therefore change the parse of code the edit never
///     touched: <c>F(a &lt; b!, c &gt; (d))</c> without its <c>!</c> is a generic invocation
///     (<c>SK2111</c>), <c>o is (1) _</c> without its discard is a constant pattern rather than a
///     positional one (<c>SK0250</c>), <c>!(a == b) switch { … }</c> rewritten to
///     <c>a != b switch { … }</c> puts the <c>switch</c> on <c>b</c> (<c>SK0260</c>), and
///     <c>a-(int)-b</c> without its cast glues into <c>a--b</c> (<c>SK0234</c>). Every one of them was a
///     safe fix that compiled, or failed to, only after it was applied (#412's audit).
///     <para>
///         ⚠ <b>The proof is the definition, as in the arranger's <c>ParenthesesRedundancy</c> (#392)</b>:
///         apply the edit to the text, re-parse, and require the result to be structurally equivalent to
///         the tree the fix intended — the original with the edited node replaced. #392 re-parsed the
///         outermost enclosing <em>expression</em>; this re-parses the <em>file</em>, incrementally, so
///         the enclosing statement, argument list, arm or pattern is covered without a list of which
///         node is far enough out, and so is a <c>switch</c> label or an attribute that no expression
///         encloses. The comparison is over the smallest enclosing member: an edit inside it cannot
///         move the parse outside it without moving the member's own span or kind, which is checked.
///     </para>
///     <para>
///         ⚠ <b>Token gluing needs no separate check</b>, and that is the point of comparing against the
///         intended tree rather than asking the replacement to parse on its own: <c>-b</c> parses alone,
///         and <c>a--b</c> is a post-decrement of <c>a</c> followed by a stray <c>b</c>, which is a
///         different tree. Skipped tokens are trivia and invisible to equivalence, so a parse error the
///         original did not have is counted separately.
///     </para>
/// </remarks>
public static class FixReparse {
    /// <summary>
    ///     Whether applying <paramref name="edits" /> parses back to the tree with
    ///     <paramref name="original" /> replaced by <paramref name="intended" />.
    /// </summary>
    /// <remarks>
    ///     The edits are the ones the diagnostic carries, in the original tree's coordinates and
    ///     non-overlapping. <paramref name="intended" /> needs the right shape and tokens; its trivia
    ///     does not matter.
    /// </remarks>
    public static bool Preserves(
        SyntaxNode original,
        SyntaxNode intended,
        CancellationToken cancellation,
        params (TextSpan Span, string Text)[] edits
    ) {
        var unit = Unit(original);
        if (!edits.All(edit => unit.Span.Contains(edit.Span))) {
            return false;
        }

        SyntaxNode expected;
        try {
            expected = unit.ReplaceNode(original, intended);
        } catch (System.InvalidCastException) {
            return false;
        }

        return Reparse(unit, edits, cancellation) is { } root
            && At(root, unit, Delta(edits)) is { } reparsed
            && Equivalent(reparsed, expected);
    }

    /// <summary>
    ///     Applies <paramref name="edits" />, requires everything outside <paramref name="scope" /> to
    ///     parse back unchanged, and returns what the parser now builds where <paramref name="scope" />
    ///     stood — or <see langword="null" /> when the edit reaches beyond it.
    /// </summary>
    /// <remarks>
    ///     For a fix whose intended shape depends on where it lands, so that the caller cannot build the
    ///     tree in advance: <c>o is string _</c> without its discard is an <c>is</c> <em>operator</em>,
    ///     <c>case Random _:</c> is a <c>case</c> label holding an expression, and <c>Random _ =&gt;</c> is a
    ///     constant pattern — and the binder, not the parser, decides what the last two mean. The caller
    ///     inspects the returned node and asks the semantic question itself. Where every edit is a
    ///     deletion, the surviving tokens must also be the original's in order, so that nothing glued.
    ///     <para>
    ///         ⚠ The edit can change the kind of the node <em>around</em> it — <c>value is string { }</c>
    ///         is an <c>is</c>-pattern expression and <c>value is string</c> an <c>is</c> operator — so the
    ///         scope widens until the node the parser built in its place slots into the original and the
    ///         rest matches. It never widens past a statement or a member.
    ///     </para>
    /// </remarks>
    public static SyntaxNode? Reparsed(
        SyntaxNode scope,
        IReadOnlyList<(TextSpan Span, string Text)> edits,
        CancellationToken cancellation
    ) {
        var unit = Unit(scope);
        if (!edits.All(edit => scope.Span.Contains(edit.Span))
            || Reparse(unit, edits, cancellation) is not { } root
            || At(root, unit, Delta(edits)) is not { } reparsedUnit) {
            return null;
        }

        var delta = Delta(edits);
        for (var current = scope; current is not null; current = current.Parent) {
            var span = new TextSpan(current.SpanStart, current.Span.Length + delta);
            if (span.Length > 0
                && reparsedUnit.FullSpan.Contains(span)
                && reparsedUnit.FindNode(span, getInnermostNodeForTie: false) is { } candidate
                && candidate.Span == span
                && Matches(unit, current, candidate, reparsedUnit)
                && SameTokens(current, candidate, edits)) {
                return candidate;
            }

            if (current == unit || current is StatementSyntax) {
                break;
            }
        }

        return null;
    }

    /// <summary>
    ///     The smallest member, or the compilation unit, that an edit inside <paramref name="node" />
    ///     is compared over: the parse outside it cannot move without moving its span or its kind.
    /// </summary>
    static SyntaxNode Unit(SyntaxNode node) =>
        node.AncestorsAndSelf()
            .FirstOrDefault(static ancestor => ancestor is MemberDeclarationSyntax
                and not BaseNamespaceDeclarationSyntax
                or CompilationUnitSyntax
            )
        ?? node.SyntaxTree.GetRoot();

    static int Delta(IReadOnlyList<(TextSpan Span, string Text)> edits) {
        var delta = 0;
        foreach (var (span, text) in edits) {
            delta += text.Length - span.Length;
        }

        return delta;
    }

    /// <summary>
    ///     The re-parsed root, or <see langword="null" /> where the edit adds a parse error — skipped
    ///     tokens are trivia, and an edit that leaves one behind would otherwise compare equal.
    /// </summary>
    static SyntaxNode? Reparse(
        SyntaxNode unit,
        IReadOnlyList<(TextSpan Span, string Text)> edits,
        CancellationToken cancellation
    ) {
        var tree = unit.SyntaxTree;
        var changed = tree.GetText(cancellation)
            .WithChanges(edits.Select(static edit => new TextChange(edit.Span, edit.Text)));
        var root = tree.WithChangedText(changed).GetRoot(cancellation);
        return Errors(root) > Errors(tree.GetRoot(cancellation)) ? null : root;
    }

    /// <summary>The node of <paramref name="unit" />'s kind standing at its moved span in <paramref name="root" />.</summary>
    static SyntaxNode? At(SyntaxNode root, SyntaxNode unit, int delta) {
        if (unit is CompilationUnitSyntax) {
            return root;
        }

        var span = new TextSpan(unit.SpanStart, unit.Span.Length + delta);
        if (span.Length <= 0 || !root.FullSpan.Contains(span)) {
            return null;
        }

        for (var node = root.FindNode(span, getInnermostNodeForTie: true);
             node is not null && node.Span == span;
             node = node.Parent) {
            if (node.RawKind == unit.RawKind) {
                return node;
            }
        }

        return null;
    }

    static bool Matches(SyntaxNode unit, SyntaxNode scope, SyntaxNode candidate, SyntaxNode reparsedUnit) {
        try {
            return Equivalent(reparsedUnit, unit.ReplaceNode(scope, candidate));
        } catch (System.InvalidCastException) {
            // The re-parsed node is of a kind the slot cannot hold; a wider scope may.
            return false;
        }
    }

    /// <summary>
    ///     Whether two trees have the same shape and the same tokens, trivia aside.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="SyntaxNode.IsEquivalentTo(SyntaxNode, bool)" />, which was the first version
    ///     and was measured to be wrong for this: a method body re-parsed incrementally compared unequal to
    ///     the same body built by <c>ReplaceNode</c> although every child compared equal — every
    ///     <c>while</c> condition rewrite was declined, and <c>SK4033</c>'s positive fixture caught it.
    ///     The pre-order walk of (depth, kind, token text, missing) is the structure by definition.
    /// </remarks>
    public static bool Equivalent(SyntaxNode left, SyntaxNode right) => Shape(left).SequenceEqual(Shape(right));

    static IEnumerable<(int Depth, int Kind, string Text, bool Missing)> Shape(SyntaxNode node) {
        var stack = new Stack<(SyntaxNodeOrToken Item, int Depth)>();
        stack.Push((node, 0));
        while (stack.Count > 0) {
            var (item, depth) = stack.Pop();
            if (item.IsToken) {
                var token = item.AsToken();
                yield return (depth, token.RawKind, token.ValueText, token.IsMissing);
                continue;
            }

            yield return (depth, item.RawKind, string.Empty, false);
            var children = item.ChildNodesAndTokens();
            for (var i = children.Count - 1; i >= 0; i--) {
                stack.Push((children[i], depth + 1));
            }
        }
    }

    /// <summary>Where every edit is a deletion, the surviving tokens must be the original's, in order.</summary>
    static bool SameTokens(SyntaxNode scope, SyntaxNode candidate, IReadOnlyList<(TextSpan Span, string Text)> edits) =>
        !edits.All(static edit => edit.Text.Length == 0)
        || scope.DescendantTokens()
            .Where(token => !edits.Any(edit => edit.Span.Contains(token.Span) && token.Span.Length > 0))
            .Select(static token => (token.RawKind, token.ValueText))
            .SequenceEqual(candidate.DescendantTokens().Select(static token => (token.RawKind, token.ValueText)));

    static int Errors(SyntaxNode root) =>
        root.ContainsDiagnostics
            ? root.GetDiagnostics().Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            : 0;
}

/// <summary>
///     ⚠ The re-bind half of proving a fix: every name a fix writes must bind, at the position it is
///     written, to the symbol the fix meant (#424).
/// </summary>
/// <remarks>
///     ⚠ A name in source is a lookup, not a reference, and the lookup runs again at the new position
///     with whatever is in scope there. #412's audit found the same omission in ten rules: a method
///     called <c>nameof</c> captures <c>nameof(x)</c> (<c>SK2017</c>), a lambda parameter called
///     <c>ex</c> captures an inserted <c>ex</c> (<c>SK2073</c>), a local named like a type captures the
///     type's shortened name (<c>SK2183</c>), a constant captures a type written into a pattern
///     (<c>SK1050</c>, <c>SK0250</c>), and an instance member captures an extension method written as
///     a call (<c>SK1080</c>, <c>SK4033</c>). Each rule had checked the symbol it was replacing and
///     none had checked the symbol it was writing.
///     <para>
///         The check is Roslyn's speculative binding: the rewritten node is bound as though it stood in
///         the original's place, inside the smallest enclosing construct Roslyn can speculate — a
///         statement, an expression body, an initializer, an attribute or a constructor initializer — so
///         that a name declared by the rewrite itself (an inserted catch variable) is in scope for the
///         names that refer to it.
///     </para>
/// </remarks>
public static class FixRebind {
    /// <summary>
    ///     <paramref name="name" /> as an identifier the parser reads back as that name: escaped with
    ///     <c>@</c> when it is a reserved keyword.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ISymbol.Name</c> is the name without its escape, so a parameter declared <c>@class</c>
    ///     is called <c>class</c>, and writing that back gives <c>nameof(class)</c> (<c>CS1026</c>,
    ///     <c>SK2017</c>). A contextual keyword needs no escape to parse, and is left to the re-bind.
    /// </remarks>
    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    /// <summary>
    ///     Binds <paramref name="replacement" /> where <paramref name="original" /> stands and hands back
    ///     the model it was bound in, with the replacement located inside it.
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> where no enclosing construct can be speculated, which a caller must
    ///     read as "not proved" and decline.
    /// </returns>
    public static bool TrySpeculate(
        SemanticModel model,
        SyntaxNode original,
        SyntaxNode replacement,
        out SemanticModel speculative,
        out SyntaxNode placed
    ) {
        var annotation = new SyntaxAnnotation();
        var annotated = replacement.WithAdditionalAnnotations(annotation);
        speculative = model;
        placed = annotated;

        for (var container = original.Parent; container is not null; container = container.Parent) {
            SemanticModel? result = null;
            SyntaxNode rewritten;
            var position = container.SpanStart;
            switch (container) {
                case StatementSyntax statement when IsInsideABody(statement):
                    rewritten = statement.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(position, (StatementSyntax)rewritten, out result);
                    break;

                case ArrowExpressionClauseSyntax arrow:
                    rewritten = arrow.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(position, (ArrowExpressionClauseSyntax)rewritten, out result);
                    break;

                // ⚠ #425: Roslyn speculates an initializer of a field, a property, a parameter or an
                // enum member, and refuses a local's — which answered "not proved" for every rewrite
                // inside `List<T> xs = …;`. A local's initializer is speculated through its statement.
                case EqualsValueClauseSyntax initializer
                    when initializer.Parent is not VariableDeclaratorSyntax {
                        Parent.Parent: not (FieldDeclarationSyntax or EventFieldDeclarationSyntax)
                    }:
                    rewritten = initializer.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(position, (EqualsValueClauseSyntax)rewritten, out result);
                    break;

                case AttributeSyntax attribute:
                    rewritten = attribute.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(position, (AttributeSyntax)rewritten, out result);
                    break;

                case ConstructorInitializerSyntax initializer:
                    rewritten = initializer.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(position, (ConstructorInitializerSyntax)rewritten, out result);
                    break;

                case PrimaryConstructorBaseTypeSyntax baseType:
                    rewritten = baseType.ReplaceNode(original, annotated);
                    model.TryGetSpeculativeSemanticModel(
                        position,
                        (PrimaryConstructorBaseTypeSyntax)rewritten,
                        out result
                    );
                    break;

                case MemberDeclarationSyntax or CompilationUnitSyntax:
                    return false;

                default:
                    continue;
            }

            if (result is null) {
                return false;
            }

            speculative = result;
            placed = result.SyntaxTree.GetRoot().GetAnnotatedNodes(annotation).FirstOrDefault() ?? annotated;
            return placed.SyntaxTree == result.SyntaxTree;
        }

        return false;
    }

    /// <summary>
    ///     Binds a statement the fix <em>inserts</em> as though it stood at <paramref name="position" />,
    ///     for a fix that moves an expression to a new statement rather than replacing one in place.
    /// </summary>
    /// <remarks>
    ///     ⚠ The scope at the insertion point is the scope of the block it lands in, which includes every
    ///     local that block declares <em>below</em> it: a name that bound to a field where it was written
    ///     binds to that local where it is moved, and is <c>CS0844</c> (<c>SK3511</c>).
    /// </remarks>
    public static bool TrySpeculate(
        SemanticModel model,
        int position,
        StatementSyntax inserted,
        out SemanticModel speculative,
        out StatementSyntax placed
    ) {
        speculative = model;
        placed = inserted;
        if (!model.TryGetSpeculativeSemanticModel(position, inserted, out var result) || result is null) {
            return false;
        }

        speculative = result;
        placed = result.SyntaxTree.GetRoot() as StatementSyntax ?? inserted;
        return placed.SyntaxTree == result.SyntaxTree;
    }

    /// <summary>
    ///     Whether every simple name in <paramref name="moved" /> binds, in <paramref name="model" />,
    ///     to what the same name bound to in <paramref name="original" />, read in order.
    /// </summary>
    /// <remarks>
    ///     For a fix that copies an expression's text to a new position: the two are the same tokens, so
    ///     the names pair up one to one.
    /// </remarks>
    public static bool BindsAlike(
        SemanticModel originalModel,
        SyntaxNode original,
        SemanticModel model,
        SyntaxNode moved,
        CancellationToken cancellation
    ) {
        var before = original.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().ToList();
        var after = moved.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().ToList();
        if (before.Count != after.Count) {
            return false;
        }

        for (var i = 0; i < before.Count; i++) {
            var was = originalModel.GetSymbolInfo(before[i], cancellation).Symbol;
            var now = model.GetSymbolInfo(after[i], cancellation).Symbol;
            if (was is null != now is null || was is not null && !SymbolEqualityComparer.Default.Equals(was, now)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether the replacement <paramref name="placed" />, bound by
    ///     <see cref="TrySpeculate(SemanticModel, SyntaxNode, SyntaxNode, out SemanticModel, out SyntaxNode)" />,
    ///     converts to what <paramref name="original" /> converted to, and leaves every expression around
    ///     it converting to the same type and binding to the same symbol, out to the speculated construct.
    /// </summary>
    /// <remarks>
    ///     ⚠ #425: for a rewrite whose result is target-typed or overload-sensitive — a collection
    ///     expression, an interpolated string, a receiver whose static type changes. Each binds by what
    ///     it is offered, so the question is not whether the new text binds but whether the call it
    ///     stands in still picks the overload it picked: #412's audit measured <c>[.. new long[] { 1 }]</c>
    ///     → <c>[1]</c> moving <c>M(List&lt;long&gt;)</c> to <c>M(List&lt;int&gt;)</c>, a <c>$""</c>
    ///     binding an interpolated-string handler <c>string.Format</c> never could, and a deleted
    ///     <c>.ToList()</c> leaving <c>ToArray()</c> to the receiver's own instance method. Symbols are
    ///     compared exactly, type arguments included: an inferred <c>T</c> that changes is a different
    ///     call even where the definition is the same.
    /// </remarks>
    public static bool BindsTheSurroundingsAlike(
        SemanticModel model,
        SyntaxNode original,
        SemanticModel speculative,
        SyntaxNode placed,
        CancellationToken cancellation
    ) {
        if (original is ExpressionSyntax
            && !SymbolEqualityComparer.Default.Equals(
                model.GetTypeInfo(original, cancellation).ConvertedType,
                speculative.GetTypeInfo(placed, cancellation).ConvertedType
            )) {
            return false;
        }

        for (SyntaxNode? was = original.Parent, now = placed.Parent;
             was is not null && now is not null;
             was = was.Parent, now = now.Parent) {
            if (was.RawKind != now.RawKind) {
                return false;
            }

            if (was is ExpressionSyntax or ArgumentSyntax
                && (!Corresponds(
                        model.GetSymbolInfo(was, cancellation).Symbol,
                        speculative.GetSymbolInfo(now, cancellation).Symbol
                    )
                    || !SymbolEqualityComparer.Default.Equals(
                        model.GetTypeInfo(was, cancellation).ConvertedType,
                        speculative.GetTypeInfo(now, cancellation).ConvertedType
                    ))) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The same symbol, or — for one the speculated construct declares itself, which the speculative
    ///     model declares afresh — one of the same kind and name.
    /// </summary>
    static bool Corresponds(ISymbol? was, ISymbol? now) =>
        SymbolEqualityComparer.Default.Equals(was, now)
        || was is ILocalSymbol
            or IParameterSymbol
            or IRangeVariableSymbol
            or IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }
        && now is not null
        && was.Kind == now.Kind
        && was.Name == now.Name;

    /// <summary>Whether <paramref name="node" /> binds, in <paramref name="model" />, to <paramref name="intended" />.</summary>
    /// <remarks>
    ///     A reduced extension method and the static method it reduces from are the same method, and a
    ///     constructed generic and its definition are compared by definition: a rewrite that keeps the
    ///     method but lets inference pick other type arguments is a different question, which the rules
    ///     that can cause it ask on their own.
    /// </remarks>
    public static bool BindsTo(
        SemanticModel model,
        SyntaxNode node,
        ISymbol? intended,
        CancellationToken cancellation
    ) =>
        intended is not null && Same(model.GetSymbolInfo(node, cancellation).Symbol, intended);

    /// <summary>Whether two symbols are the same declaration.</summary>
    public static bool Same(ISymbol? left, ISymbol? right) =>
        left is not null
        && right is not null
        && SymbolEqualityComparer.Default.Equals(Definition(left), Definition(right));

    static ISymbol Definition(ISymbol symbol) =>
        symbol switch {
            IMethodSymbol { ReducedFrom: { } reduced } => reduced.OriginalDefinition,
            _ => symbol.OriginalDefinition
        };

    /// <summary>
    ///     What a simple name written as an <em>expression</em> at <paramref name="position" /> binds to.
    /// </summary>
    /// <remarks>
    ///     ⚠ A pattern is bound as an expression first and as a type only if that fails, so
    ///     <c>case Random:</c> and <c>is not Kind</c> mean a constant called <c>Random</c> or <c>Kind</c>
    ///     wherever one is in scope, although <c>case Random _:</c> and <c>is Kind</c> meant the type.
    /// </remarks>
    public static ISymbol? AsExpression(SemanticModel model, int position, TypeSyntax written) =>
        model.GetSpeculativeSymbolInfo(
                position,
                SyntaxFactory.ParseTypeName(written.ToString()),
                SpeculativeBindingOption.BindAsExpression
            )
            .Symbol;

    /// <summary>
    ///     Whether a member lookup of <paramref name="name" /> in <paramref name="container" /> finds
    ///     anything other than <paramref name="removed" /> — what the name binds to once the fix deletes
    ///     <paramref name="removed" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ A deletion writes no name but changes what one binds to: a record's positional property is
    ///     synthesized only where no member of that name is declared <em>or inherited</em>, so deleting
    ///     an explicit <c>X</c> under a base that declares one makes <c>d.X</c> the base's (<c>SK0282</c>,
    ///     #412's audit, <c>1</c> → <c>10</c>).
    /// </remarks>
    public static bool FindsAnotherMember(
        SemanticModel model,
        int position,
        INamespaceOrTypeSymbol container,
        string name,
        ISymbol removed
    ) =>
        model.LookupSymbols(position, container, name)
            .Any(symbol => !Same(symbol, removed) && symbol.Kind is not (SymbolKind.NamedType or SymbolKind.Namespace));

    static bool IsInsideABody(StatementSyntax statement) =>
        statement.Parent is BlockSyntax
            or StatementSyntax
            or SwitchSectionSyntax
            or GlobalStatementSyntax
            or ElseClauseSyntax
            or CatchClauseSyntax
            or FinallyClauseSyntax
        || statement is BlockSyntax {
            Parent:
            BaseMethodDeclarationSyntax
            or AccessorDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AnonymousFunctionExpressionSyntax
        };
}
