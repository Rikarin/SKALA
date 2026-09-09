using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     Whether a node sits inside a lambda that the compiler turns into an
///     <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> rather than into a delegate.
/// </summary>
/// <remarks>
///     ⚠ This is a <b>layer 1</b> precondition — a property of the rewrite itself — and it is shared
///     because it is not one rule's problem. An expression tree is a restricted sub-language: it may not
///     contain an <c>is</c> pattern (CS8122), a <c>switch</c> expression (CS8514), a deconstruction
///     (CS8143), an assignment such as <c>??=</c> (CS0832), an out-variable declaration (CS8198), or a
///     local function. <b>Any</b> arrangement rule that introduces one of those into a lambda body needs
///     this check, whatever else it has already proved about the code it is rewriting.
///     <para>
///         ⚠ The operand's type is irrelevant here, and that is what makes this a second precondition
///         rather than a wrinkle in the first. <see cref="NullCheckingPatternRule" /> already refuses an
///         operand whose type declares <c>operator ==</c>; the case in #347 is <c>string?</c>, whose
///         operator is the one that check deliberately allows. The rewrite is illegal because of
///         <em>where it is</em>, not because of what it binds to.
///     </para>
///     <para>
///         ⚠ Relying on the safety net instead is not a cheaper equivalent. <c>ArrangementSafety</c>
///         layer 2 catches the CS8122 by re-binding, but its unit is the <em>file</em>: it reverts every
///         rewrite in it, so one expression-tree null check discards the unrelated correct rewrites
///         beside it and the file can never be arranged at all — it re-drops the same crash artefact on
///         every run. Refusing the single rewrite is what keeps the rest of the file.
///     </para>
/// </remarks>
public static class ExpressionTreeContext {
    /// <summary>
    ///     Whether <paramref name="node" /> is inside an expression-tree lambda.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Every</b> enclosing lambda is examined, not just the nearest one, and this is measured
    ///     rather than assumed. In
    ///     <c>Expression&lt;Func&lt;Row, bool&gt;&gt; e = r =&gt; Apply(r, x =&gt; x.Banner == null);</c>
    ///     the inner lambda's own <c>ConvertedType</c> is <c>Func&lt;Row, bool&gt;</c> — not an
    ///     <c>Expression</c> at all — yet <c>csc</c> still reports CS8122 on a pattern written in its
    ///     body, because the whole tree is compiled as data. Stopping at the first lambda lets exactly
    ///     that shape through.
    ///     <para>
    ///         The walk stops at a member or local-function boundary: past it the node is no longer
    ///         lexically inside any lambda that could have been converted, so continuing to the
    ///         compilation unit would only cost time.
    ///     </para>
    /// </remarks>
    public static bool Contains(SemanticModel model, SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case LambdaExpressionSyntax or AnonymousMethodExpressionSyntax:
                    if (IsExpressionTreeType(model.GetTypeInfo(current).ConvertedType)) {
                        return true;
                    }

                    break;

                // A lambda cannot enclose the node from outside its own member, and a local function
                // body is a delegate body rather than tree data.
                case MemberDeclarationSyntax or LocalFunctionStatementSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether <paramref name="type" /> is, or derives from, <c>System.Linq.Expressions.Expression</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ The base chain is walked because the converted type is the constructed
    ///     <c>Expression&lt;TDelegate&gt;</c>, which reaches the non-generic <c>Expression</c> only
    ///     through <c>LambdaExpression</c>. Matching the namespace symbol by hand rather than resolving
    ///     the metadata name keeps this working under <c>--load=loose</c>, where the compilation's
    ///     references are whatever the shared framework supplied and a lookup can come back null — and a
    ///     null lookup would silently answer "not an expression tree" for every file.
    /// </remarks>
    static bool IsExpressionTreeType(ITypeSymbol? type) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current is {
                    Name: "Expression",
                    ContainingType: null,
                    ContainingNamespace: {
                        Name: "Expressions",
                        ContainingNamespace: {
                            Name: "Linq",
                            ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
                        }
                    }
                }) {
                return true;
            }
        }

        return false;
    }
}
