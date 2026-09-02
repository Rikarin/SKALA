using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
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

    /// <summary>
    ///     The BCL's vocabulary for "this call changes the thing it is called on".
    /// </summary>
    /// <remarks>
    ///     ⚠ A vocabulary, not a proof. <c>Replace</c> is deliberately absent because
    ///     <c>string.Replace</c> is pure and <c>StringBuilder.Replace</c> is not, and a name that means
    ///     two things is worse than a name that is missing: the guard's job is to decline the shapes
    ///     that read as mutation, and a false decline costs one hint.
    /// </remarks>
    static readonly HashSet<string> Mutators = new(StringComparer.Ordinal) {
        "Add", "AddOrUpdate", "AddRange", "Append", "AppendFormat", "AppendLine", "Clear", "Dequeue", "Enqueue",
        "GetOrAdd", "Insert", "InsertRange", "MoveNext", "Next", "NextDouble", "Pop", "Push", "Remove", "RemoveAll",
        "RemoveAt", "RemoveRange", "Reset", "Set", "SetValue", "Sort", "TryAdd", "TryDequeue", "TryPop", "TryRemove",
        "TryTake", "TryUpdate", "Write", "WriteLine"
    };

    /// <summary>The attributes by which a method proves something about a null state.</summary>
    static readonly HashSet<string> NullStateAttributes = new(StringComparer.Ordinal) {
        "NotNullWhenAttribute", "MaybeNullWhenAttribute", "NotNullIfNotNullAttribute", "DoesNotReturnIfAttribute",
        "MemberNotNullAttribute", "MemberNotNullWhenAttribute"
    };

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

        if (HasASideEffect(guard.Condition, model, cancellation)) {
            return;
        }

        if (NarrowsSomethingTheBodyUses(guard.Condition, guard.Statement, model, cancellation)) {
            return;
        }

        if (EveryPathJumpsOutOfTheBody(guard.Statement, model)) {
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

    /// <summary>
    ///     Whether the condition mutates anything, which a filter predicate must not (#329).
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The rewrite stays behaviourally correct and is still declined, and that is the point.</b>
    ///     <c>Where</c> is lazy, so <c>options.Where(o =&gt; !used.Add(Leaf(o.Key)))</c> runs the
    ///     predicate once per element in order, exactly as the <c>if</c> did — the <c>HashSet</c> ends up
    ///     the same. What the edit produces is a filter whose predicate has a side effect, which any
    ///     later <c>.ToList()</c>, <c>.Count()</c> or second enumeration silently changes. Moving a
    ///     mutation somewhere its number of evaluations stops being obvious is not a modernization.
    ///     <para>
    ///         ⚠ <b>Purity is undecidable and this does not pretend otherwise</b> — it is the BCL's
    ///         mutator vocabulary plus the three shapes that mutate whatever they name: an assignment,
    ///         an increment, and a <c>ref</c>/<c>out</c> argument. A mutator spelled some other way gets
    ///         through, which is why the rule ships at <c>hint</c> with <c>fixIsSafe: false</c>.
    ///     </para>
    /// </remarks>
    static bool HasASideEffect(ExpressionSyntax condition, SemanticModel model, CancellationToken cancellation) {
        foreach (var node in condition.DescendantNodesAndSelf()) {
            switch (node) {
                case AssignmentExpressionSyntax:
                    return true;

                case PrefixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
                }:
                case PostfixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
                }:
                    return true;

                case ArgumentSyntax argument when argument.RefKindKeyword.RawKind != (int)SyntaxKind.None:
                    return true;

                case InvocationExpressionSyntax invocation
                    when model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol target
                    && Mutators.Contains(target.Name):
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the condition establishes a null state the body then relies on (#329).
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>An <c>if</c> narrows for its body and a <c>Where</c> predicate does not, and the two
    ///     rewrites are otherwise the same tokens.</b> <c>option.Default is not null</c> in the guard
    ///     proves <c>option.Default</c> non-null inside the guard's body; in a lambda handed to
    ///     <c>Where</c> it proves nothing about the loop body, and the call that consumed it becomes
    ///     CS8604. Measured: 6 of 133 insertions on this repository moved such a condition, and only one
    ///     of them broke the build — the other five compile because the value happens not to be used
    ///     where non-nullness is required, so a count of build errors was never the measure of this.
    ///     <para>
    ///         ⚠ <b>Nullability decides, not syntax.</b> A null test over something the compiler already
    ///         types as non-nullable narrows nothing, so declining it would cost findings and buy
    ///         nothing — see <see cref="CouldBeNull" /> for which signal answers that and which one
    ///         looked like it would and does not.
    ///     </para>
    ///     <para>
    ///         ⚠ A method with a nullable post-condition attribute — <c>NotNullWhen</c> and its
    ///         relatives, which is how <c>string.IsNullOrEmpty</c> narrows — is treated as narrowing
    ///         everything it is handed. Reading only <c>is null</c> and <c>!= null</c> would have missed
    ///         the whole family.
    ///     </para>
    /// </remarks>
    static bool NarrowsSomethingTheBodyUses(
        ExpressionSyntax condition,
        StatementSyntax body,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var used = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var node in body.DescendantNodesAndSelf()) {
            if (node is SimpleNameSyntax name && model.GetSymbolInfo(name, cancellation).Symbol is { } symbol) {
                used.Add(symbol);
            }
        }

        foreach (var narrowed in Narrowed(condition, model, cancellation)) {
            if (!CouldBeNull(narrowed, model, cancellation)) {
                continue;
            }

            foreach (var node in narrowed.DescendantNodesAndSelf()) {
                if (node is SimpleNameSyntax name
                    && model.GetSymbolInfo(name, cancellation).Symbol is { } symbol
                    && used.Contains(symbol)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether narrowing this expression could tell the body something it does not already know.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The declared annotation, and <c>Nullability.FlowState</c> was tried first and is
    ///     useless here — measured, not assumed.</b> Asked of the operand of an <c>is</c> pattern,
    ///     Roslyn answers <c>MaybeNull</c> for every expression there is: a non-nullable property, a
    ///     non-nullable parameter and a nullable one all come back the same, because the question being
    ///     answered is the pattern's, not the program's. A guard built on it declines everything.
    ///     <para>
    ///         ⚠ <b>A <c>var</c> iteration variable is <c>Annotated</c> whatever it iterates, and that
    ///         is right rather than a limitation.</b> <c>var</c> infers the annotated form and leaves
    ///         non-nullness to the flow state — which is exactly the flow state a <c>Where</c> predicate
    ///         does not carry into the body, so <c>if (item is not null) { Use(item); }</c> really would
    ///         become CS8604.
    ///     </para>
    ///     <para>
    ///         <c>None</c> — a file with nullable analysis off — allows the rewrite: there is no
    ///         narrowing there to lose. An expression whose symbol does not resolve declines.
    ///     </para>
    /// </remarks>
    static bool CouldBeNull(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellation) {
        var symbol = model.GetSymbolInfo(expression, cancellation).Symbol;
        if (symbol is ITypeSymbol or INamespaceSymbol) {
            return false;
        }

        var type = symbol switch {
            ILocalSymbol local => local.Type,
            IParameterSymbol parameter => parameter.Type,
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            IMethodSymbol method => method.ReturnType,
            _ => model.GetTypeInfo(expression, cancellation).Type
        };

        return type is null
            || !type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated;
    }

    /// <summary>Every expression whose null state the condition could establish.</summary>
    static IEnumerable<ExpressionSyntax> Narrowed(
        ExpressionSyntax condition,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        foreach (var node in condition.DescendantNodesAndSelf()) {
            switch (node) {
                case IsPatternExpressionSyntax pattern:
                    yield return pattern.Expression;
                    break;

                case BinaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression
                } comparison:
                    if (comparison.Right.IsKind(SyntaxKind.NullLiteralExpression)) {
                        yield return comparison.Left;
                    }

                    if (comparison.Left.IsKind(SyntaxKind.NullLiteralExpression)) {
                        yield return comparison.Right;
                    }

                    break;

                case InvocationExpressionSyntax invocation
                    when model.GetSymbolInfo(invocation, cancellation).Symbol is IMethodSymbol target
                    && AnnouncesANullState(target):
                    if (invocation.Expression is MemberAccessExpressionSyntax access) {
                        yield return access.Expression;
                    }

                    foreach (var argument in invocation.ArgumentList.Arguments) {
                        yield return argument.Expression;
                    }

                    break;
            }
        }
    }

    /// <summary>Whether the method's annotations let it prove something null or non-null.</summary>
    static bool AnnouncesANullState(IMethodSymbol target) {
        if (Announces(target.GetAttributes())) {
            return true;
        }

        foreach (var parameter in target.Parameters) {
            if (Announces(parameter.GetAttributes())) {
                return true;
            }
        }

        return false;
    }

    static bool Announces(ImmutableArray<AttributeData> attributes) {
        foreach (var attribute in attributes) {
            if (attribute.AttributeClass is { } type && NullStateAttributes.Contains(type.Name)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether control cannot reach the end of the guard's body, which makes the rewrite a
    ///     single-iteration loop (#329).
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The fix would hand the author another rule's finding.</b>
    ///     <c>foreach (var d in tree.GetDiagnostics()) { if (d.Severity == Error) { return null; } }</c>
    ///     rewrites to a <c>foreach</c> whose entire body is <c>return null;</c>, which is <c>SK2212</c>
    ///     — a loop that cannot run twice. The tree had zero <c>SK2212</c> before the fix ran and would
    ///     have had one per rewrite of this shape.
    ///     <para>
    ///         ⚠ <b>Declined rather than rewritten to <c>Any(…)</c>.</b> The right rewrite is an
    ///         <c>if</c> over <c>Any</c> when the body ignores the element and a <c>FirstOrDefault</c>
    ///         when it does not, and choosing between them is not a decision a text edit over the loop
    ///         header can take. ⚠ This class is invisible to
    ///         <c>EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c>, which filters the post-fix
    ///         diagnostics to the fixture's own rule id, so a fixture is the only thing that can pin it.
    ///     </para>
    ///     <para>
    ///         The question is asked of the compiler — the same question <c>SK2212</c> asks — rather
    ///         than by matching <c>return</c>, so <c>{ Log(x); return null; }</c> and a body ending in
    ///         <c>throw</c> are caught alongside the bare jump.
    ///     </para>
    /// </remarks>
    static bool EveryPathJumpsOutOfTheBody(StatementSyntax body, SemanticModel model) =>
        model.AnalyzeControlFlow(body) is { Succeeded: true, EndPointIsReachable: false };

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
