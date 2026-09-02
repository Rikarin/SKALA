using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     The guards the <c>SK1xxx</c> rewrites share, in one place because they are the rules.
/// </summary>
/// <remarks>
///     ⚠ Every rule in this namespace turns one shape of code into another, so each of them has to
///     answer the same three questions before it may fire: does the rewrite change how many times
///     something is <em>evaluated</em>, does it delete text a person <em>wrote</em>, and does the
///     result still <em>compile</em>. A rule that answers any of them wrong is not a noisy rule, it is
///     a wrong one — docs/plan/16 § R3's distinction — so the answers live here rather than being
///     re-derived, slightly differently, eight times.
/// </remarks>
internal static class RewriteGuards {
    /// <summary>
    ///     Whether an expression is a chain of plain names — <c>x</c>, <c>this.x</c>, <c>a.b.c</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same predicate <c>SK1030</c> needs, and for the same reason. No invocation, no element
    ///     access and no <c>await</c> anywhere in it: each can have a side effect, and every rule here
    ///     either evaluates such an expression a different number of times than the original did or
    ///     moves it to a different point in the program. On a chain of names both are free.
    ///     <para>
    ///         ⚠ A property access is a method call and is <em>not</em> free in general. It is admitted
    ///         anyway, because excluding it would silence every rule here on <c>this.Items</c> and
    ///         <c>Options.Map</c>, which is most of their value — and because no rule in this namespace
    ///         moves such an expression across a statement boundary: the rewrites collapse two adjacent
    ///         evaluations in the same statement into one. A property whose getter is not idempotent
    ///         between two adjacent reads is already a bug the rule is reporting rather than causing.
    ///     </para>
    /// </remarks>
    public static bool IsPlainNamePath(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                case PredefinedTypeSyntax:
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    expression = access.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }

    /// <summary>Whether two expressions are the same text, ignoring trivia.</summary>
    public static bool Same(ExpressionSyntax left, ExpressionSyntax right) =>
        SyntaxFactory.AreEquivalent(left, right, false);

    /// <summary>
    ///     ⚠ Whether a span a fix is about to delete or rewrite contains something a person wrote.
    /// </summary>
    /// <remarks>
    ///     A comment inside the text a rewrite removes is content, and a fix that silently deletes it
    ///     is a fix nobody can review. A preprocessor directive is worse: removing one half of an
    ///     <c>#if</c> does not merely lose text, it stops the file parsing under the other symbol set.
    /// </remarks>
    public static bool ContainsCommentOrDirective(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                || trivia.IsDirective) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The same question over a raw span, for a rewrite that deletes part of a node.</summary>
    public static bool ContainsCommentOrDirective(SyntaxTree tree, TextSpan span) {
        var text = tree.GetText().ToString(span);
        return text.IndexOf("//", System.StringComparison.Ordinal) >= 0
            || text.IndexOf("/*", System.StringComparison.Ordinal) >= 0
            || text.IndexOf('#') >= 0;
    }

    /// <summary>
    ///     The full span of a statement including the trivia that would be orphaned by deleting it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deleting <c>statement.Span</c> alone leaves the indentation and the newline behind, and
    ///     while <c>skala fix</c> re-formats every file it touches (docs/plan/08 § FixEdits), a blank
    ///     line is not something the formatter is allowed to remove — <c>keep_blank_lines_in_code</c>
    ///     preserves what the author wrote. So the fix removes the line rather than the statement.
    /// </remarks>
    public static TextSpan LineSpanOf(StatementSyntax statement) =>
        TextSpan.FromBounds(statement.FullSpan.Start, statement.FullSpan.End);

    /// <summary>
    ///     Whether introducing a local called <paramref name="name" /> at <paramref name="position" />
    ///     would collide with something already in scope.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the guard that stops a rewrite producing <c>CS0128</c>/<c>CS0136</c>. Two of the
    ///     rules here move a declaration outwards — <c>SK1006</c> lifts a <c>using</c> block's
    ///     statements into the enclosing block, <c>SK1015</c> and <c>SK1033</c> move a declaration into
    ///     an enclosing condition — and C# forbids a local that shadows a local of an enclosing local
    ///     scope in the same member. <c>LookupSymbols</c> at the destination is exactly the set that
    ///     would conflict, which is why the question is asked there rather than at the source.
    /// </remarks>
    public static bool WouldCollide(
        SemanticModel model,
        int position,
        string name,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();
        foreach (var symbol in model.LookupSymbols(position, name: name)) {
            if (symbol is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol) {
                return true;
            }

            if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether a name being lifted out of <paramref name="moved" /> is declared by any other local
    ///     scope of the same member.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is the second half of the scoping guard and the half that <c>LookupSymbols</c> cannot
    ///     answer. Three rules here move a declaration one scope outwards — <c>SK1006</c> lifts a
    ///     <c>using</c> block's statements, <c>SK1015</c> and <c>SK1033</c> move a declaration into a
    ///     condition, where C# scopes the pattern variable to the <em>enclosing block</em>. A name in a
    ///     scope that merely <em>neighbours</em> the destination is not in scope at the destination and
    ///     so is invisible to a lookup, yet it is exactly what <c>CS0136</c> is about:
    ///     <code>
    /// if (c) { var t = 1; }            // legal today: two cousins
    /// if (x is T) { var t = (T)x; }    // CS0136 once `t` is lifted into the enclosing block
    ///     </code>
    ///     <para>
    ///         ⚠ It over-bails, deliberately. Scanning the whole member counts names that could never
    ///         conflict — two sibling lambdas, a name inside a nested type's initializer — and each one
    ///         costs a finding. The alternative is a fix that does not compile, and docs/plan/10 is explicit
    ///         that a fixing tool which can break the build is one an agent will use to break the build.
    ///     </para>
    /// </remarks>
    public static bool DeclaredElsewhereInMember(SyntaxNode moved, string name) {
        var root = ScopeRoot(moved);
        foreach (var node in root.DescendantNodes()) {
            if (node.Span.OverlapsWith(moved.Span)) {
                continue;
            }

            foreach (var declared in DeclaredNames(node)) {
                if (string.Equals(declared, name, System.StringComparison.Ordinal)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Every name a syntax node declares into some local scope.</summary>
    public static IEnumerable<string> DeclaredNames(SyntaxNode node) {
        switch (node) {
            case VariableDeclaratorSyntax declarator:
                yield return declarator.Identifier.ValueText;
                break;

            case SingleVariableDesignationSyntax designation:
                yield return designation.Identifier.ValueText;
                break;

            case ForEachStatementSyntax forEach:
                yield return forEach.Identifier.ValueText;
                break;

            case LocalFunctionStatementSyntax function:
                yield return function.Identifier.ValueText;
                break;

            case CatchDeclarationSyntax { Identifier.ValueText.Length: > 0 } declaration:
                yield return declaration.Identifier.ValueText;
                break;

            case ParameterSyntax { Identifier.ValueText.Length: > 0 } parameter:
                yield return parameter.Identifier.ValueText;
                break;
        }
    }

    /// <summary>The member a statement lives in — as far outwards as a local name can conflict.</summary>
    /// <remarks>
    ///     ⚠ It deliberately does <b>not</b> stop at a lambda or a local function. A lambda body is a
    ///     nested local scope of the method containing it, so a local of that method conflicts with one
    ///     lifted into the lambda's block, and a root that stopped at the lambda would never see it.
    /// </remarks>
    public static SyntaxNode ScopeRoot(SyntaxNode node) {
        var current = node;
        while (current.Parent is not null) {
            current = current.Parent;
            if (current is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or PropertyDeclarationSyntax
                or IndexerDeclarationSyntax
                or BaseFieldDeclarationSyntax
                or CompilationUnitSyntax) {
                return current;
            }
        }

        return current;
    }

    /// <summary>
    ///     Whether the local is named anywhere in its scope other than its own declaration and one
    ///     node the caller is about to fold it into.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shared rather than duplicated: <c>SK1024</c> asked it as "mentioned outside" and
    ///     <c>SK1042</c> as "referenced only within", the same walk written twice with opposite
    ///     polarity. Both rules move a declaration into another construct, and both are only safe when
    ///     the answer is no — so the two copies had to agree, and nothing made them.
    ///     <para>
    ///         The name is compared before the symbol on purpose. Resolving every identifier in the
    ///         scope is what this would otherwise cost, and a local's name is a cheap, exact filter.
    ///     </para>
    /// </remarks>
    public static bool ReferencedOutside(
        SemanticModel model,
        ILocalSymbol local,
        SyntaxNode allowed,
        SyntaxNode declaration,
        CancellationToken cancellation
    ) {
        foreach (var node in ScopeRoot(declaration).DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, local.Name, System.StringComparison.Ordinal)
                || allowed.Span.Contains(identifier.Span)
                || declaration.Span.Contains(identifier.Span)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellation).Symbol, local)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>A message fragment that will not run off the end of a terminal.</summary>
    public static string Trim(string value) => value.Length <= 48 ? value : value.Substring(0, 48) + "…";
}
