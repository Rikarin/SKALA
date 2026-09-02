using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1084</c> — a <c>foreach</c> whose whole body is one <c>if</c> has a filter in it.
/// </summary>
/// <remarks>
///     <para>
///         The loop header says <em>what is visited</em> and the body says <em>what is done</em>. A body
///         that is nothing but a guard has put half of the first sentence into the second; moving the
///         guard to the sequence leaves each construct saying one thing.
///     </para>
///     <para>
///         ⚠ <b><c>Where</c> is lazy, and that is the whole equivalence argument.</b> Its
///         <c>MoveNext</c> pulls one element, evaluates the predicate and yields, so the predicate for
///         element <i>n</i> runs after the body for element <i>n-1</i> and immediately before the body
///         for element <i>n</i> — the same interleaving, in the same order, the same number of times, as
///         the <c>if</c> it replaced. A condition reading state the body mutates still sees exactly what
///         it saw.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A <c>break</c>, a <c>continue</c> or a <c>return</c> in the body does not prevent this
///             rewrite, and the belief that it does is what made the shape look unshippable.
///         </b> Only
///         the filter moves; the body stays a loop body, so <c>continue</c> still advances the same
///         <c>foreach</c> and <c>break</c> still leaves it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Reusing the loop variable's name as the lambda parameter is legal, and that was
///             measured rather than assumed.
///         </b> The iteration variable's scope does not reach the
///         collection expression, so <c>foreach (var x in xs.Where(x =&gt; x &gt; 0))</c> compiles — which
///         is what lets the condition move across as source text, unrewritten.
///     </para>
///     <para>
///         ⚠ <b>It is the one rule in this range that suggests LINQ, so it declares the interaction.</b>
///         The fix produces no materialization, so it cannot feed <c>SK4006</c>. <c>SK4001</c> reports
///         LINQ in a configured hot path and there the two genuinely disagree — settled by this rule
///         shipping at <c>hint</c>, one step below anything <c>SK4001</c> says.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoopFilterAsQueryAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.LoopFilterAsQuery);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LoopFilterAsQuery);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.ForEachStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var loop = (ForEachStatementSyntax)context.Node;

        // ⚠ `await foreach` is a different operator over a different interface; `Where` is not it.
        if (!loop.AwaitKeyword.IsKind(SyntaxKind.None)) {
            return;
        }

        if (Guard(loop.Statement) is not { } guard || guard.Else is not null) {
            return;
        }

        // ⚠ A guard whose own body is another lone `if` would make this rule fire on its own output,
        // and `EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic` is the test that says so. Two nested
        // filters are one rewrite a person should make deliberately, not two the tool applies in a loop.
        if (Guard(guard.Statement) is not null) {
            return;
        }

        // ⚠ Appending `.Where(…)` to the collection expression only means what it looks like when the
        // expression already binds tighter than a member access. `a ?? b` and `x as T` do not.
        if (!IsAppendable(loop.Expression)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetDeclaredSymbol(loop, cancellation) is not { } variable) {
            return;
        }

        if (!MentionsVariable(guard.Condition, model, variable, cancellation)) {
            return;
        }

        if (!IsLiftableIntoALambda(guard.Condition, model, cancellation)) {
            return;
        }

        var sequence = model.GetTypeInfo(loop.Expression, cancellation).Type;
        if (sequence is null || !HasWhereInScope(model, loop.Expression.SpanStart, sequence, enumerable)) {
            return;
        }

        // Two edits: the filter joins the sequence, and the guard's own statement takes the body's
        // place. Both carry source text across, so comments inside the surviving parts are kept and a
        // comment inside a part that would be *deleted* withdraws the finding.
        var head = TextSpan.FromBounds(guard.IfKeyword.SpanStart, guard.CloseParenToken.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(loop.SyntaxTree, head)) {
            return;
        }

        var text = loop.SyntaxTree.GetText(cancellation);
        var kept = guard.Statement;
        var body = loop.Statement;
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(
                loop.SyntaxTree,
                TextSpan.FromBounds(kept.Span.End, body.Span.End)
            )) {
            return;
        }

        var name = loop.Identifier.ValueText;
        var predicate = ".Where(" + name + " => " + guard.Condition + ")";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(loop.SyntaxTree, head),
                FixEdits.Pack(
                    (new TextSpan(loop.Expression.Span.End, 0), predicate),
                    (body.Span, text.ToString(kept.Span))
                ),
                "The loop's whole body is a filter: `in " + RewriteGuards.Trim(loop.Expression + predicate) + "`"
            )
        );
    }

    /// <summary>The single <c>if</c> a statement consists of, whether or not it is braced.</summary>
    static IfStatementSyntax? Guard(StatementSyntax statement) =>
        statement switch {
            IfStatementSyntax bare => bare,
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as IfStatementSyntax,
            _ => null
        };

    static bool IsAppendable(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ParenthesizedExpressionSyntax;

    static bool MentionsVariable(
        ExpressionSyntax condition,
        SemanticModel model,
        ISymbol variable,
        CancellationToken cancellation
    ) {
        foreach (var node in condition.DescendantNodesAndSelf()) {
            if (node is IdentifierNameSyntax identifier
                && SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellation).Symbol,
                    variable
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the condition is one the compiler would accept inside a lambda.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is a compile question and is asked as one rather than reasoned about. A declaration
    ///     the body then reads would move out of scope; a <c>ref</c>, <c>out</c> or <c>in</c> parameter,
    ///     a ref local, a <c>ref struct</c> value, a <c>stackalloc</c> and a pointer are each a lambda
    ///     the compiler rejects outright (<c>CS1628</c>, <c>CS8175</c>, <c>CS1686</c>), and
    ///     <c>this</c> inside a struct is <c>CS1673</c>. A fix that does not compile is worse than the
    ///     shape it replaced.
    /// </remarks>
    static bool IsLiftableIntoALambda(
        ExpressionSyntax condition,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        foreach (var node in condition.DescendantNodesAndSelf()) {
            switch (node) {
                case SingleVariableDesignationSyntax:
                case DeclarationExpressionSyntax:
                case StackAllocArrayCreationExpressionSyntax:
                case ImplicitStackAllocArrayCreationExpressionSyntax:
                case AwaitExpressionSyntax:
                case PointerTypeSyntax:
                case RefExpressionSyntax:
                    return false;

                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.AddressOfExpression }:
                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PointerIndirectionExpression }:
                    return false;

                case ThisExpressionSyntax:
                    if (model.GetTypeInfo(node, cancellation).Type is { IsValueType: true }) {
                        return false;
                    }

                    break;

                case IdentifierNameSyntax identifier: {
                    var symbol = model.GetSymbolInfo(identifier, cancellation).Symbol;
                    if (symbol is IParameterSymbol { RefKind: not RefKind.None }
                        or ILocalSymbol { RefKind: not RefKind.None }) {
                        return false;
                    }

                    // ⚠ A `ref struct` value cannot be captured at all — `Span<T>` is the one everybody
                    // meets — and an implicit `this` reaches an instance member of the enclosing struct,
                    // which is CS1673 without a `this` token to match on.
                    if (symbol is ILocalSymbol { Type.IsRefLikeType: true }
                        or IParameterSymbol { Type.IsRefLikeType: true }) {
                        return false;
                    }

                    if (IsImplicitThisMemberOfAStruct(identifier, symbol)) {
                        return false;
                    }

                    break;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether a bare name reaches an instance member of the enclosing <em>struct</em>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>CS1673</c>: a lambda inside a struct cannot touch <c>this</c>, and an unqualified
    ///     member name is a <c>this</c> reference with no token to match on. A name that is the
    ///     <c>Name</c> half of a member access is qualified by something else and is not this case.
    /// </remarks>
    static bool IsImplicitThisMemberOfAStruct(IdentifierNameSyntax identifier, ISymbol? symbol) {
        if (symbol is not (IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol)
            || symbol.IsStatic
            || symbol.ContainingType is not { IsValueType: true }) {
            return false;
        }

        return identifier.Parent is not MemberAccessExpressionSyntax {
            RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
        } access
            || access.Expression == identifier;
    }

    /// <summary>
    ///     Whether <c>Enumerable.Where</c> is reachable as an extension at this position.
    /// </summary>
    /// <remarks>
    ///     ⚠ The fix does not add a <c>using</c> directive, so a file without <c>System.Linq</c> is left
    ///     alone rather than broken. Asking the model what is in scope at the position answers that for
    ///     file-scoped, global and implicit usings alike, where reading the file's own directives would
    ///     answer it only for one of the three.
    /// </remarks>
    static bool HasWhereInScope(
        SemanticModel model,
        int position,
        ITypeSymbol sequence,
        INamedTypeSymbol enumerable
    ) {
        if (sequence is not INamespaceOrTypeSymbol container) {
            return false;
        }

        foreach (var symbol in model.LookupSymbols(
                     position,
                     container,
                     "Where",
                     true
                 )) {
            if (symbol is IMethodSymbol method
                && SymbolEqualityComparer.Default.Equals(
                    (method.ReducedFrom ?? method).OriginalDefinition.ContainingType,
                    enumerable
                )) {
                return true;
            }
        }

        return false;
    }
}
