using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>this.field</c> ⇔ <c>field</c>, under the four <c>dotnet_style_qualification_for_*</c> keys.
/// </summary>
/// <remarks>
///     ⚠ The rule runs in both directions and the direction is chosen per member kind, because that is
///     what the oracle does. Measured against <c>jb cleanupcode</c> 2025.2.6 under the cleanup profile,
///     one key at a time over a probe carrying a bare and a <c>this.</c>-qualified reference to a field,
///     a property, a method and an event: <c>dotnet_style_qualification_for_field = true</c> writes
///     <c>this._field</c> and touches nothing else, and the other three behave the same for their own
///     kind. The export writes <c>false</c> for all four, which is why only the removing direction is
///     visible in the committed fixtures.
///     <para>
///         ⚠
///         <b>
///             There used to be a fifth key here, and it was measured wrong for a long time before it
///             was measured right.
///         </b> <c>resharper_remove_this_qualifier</c> gated the removing direction.
///         The same probe with it at <c>false</c> came back byte-identical — the qualifier still
///         removed — which said it was dominated by the four Roslyn keys; that was recorded as
///         SK-DIV-0070 and the key was kept anyway, because it was Tier A and a fixture claimed it.
///         The first cleanup-profile sweep then returned <c>SPURIOUS</c> for it: Skala's output varied
///         across its values while the oracle's did not. It was removed from the export and the
///         registry on 2026-08-29 as a key ReSharper does not support, and regenerating all 1336
///         fixtures against the oracle changed <b>not one non-header line</b> — which is what a key
///         nothing reads looks like when you finally take it out.
///     </para>
/// </remarks>
public sealed class ThisQualifierRule : ArrangementRule {
    public override string Id => ArrangeIds.ThisQualifier;

    /// <summary>
    ///     ⚠ Semantic, and it is worth saying why, because <c>this.x</c> ⇒ <c>x</c> looks like a string
    ///     edit. Removing the qualifier changes the set of things the bare name can bind to: a local, a
    ///     parameter, a static of the same name, a using-imported extension. The rewrite is only legal
    ///     when the bare name binds to the same symbol, and that is a question only the model answers.
    ///     It is also the reason this rule is on layer 3's list in doc 06 § "Safety".
    /// </summary>
    public override bool NeedsSemantics => true;

    // ⚠ Unconditional since `resharper_remove_this_qualifier` was removed from the registry: the
    // rule has to run whether the per-kind keys ask for a qualifier or its removal, and the old
    // first disjunct was a key ReSharper does not read (SK-DIV-0070, and the sweep's SPURIOUS
    // verdict on it — Skala's output varied across its values while the oracle's did not).
    // ⚠ `true`, not a disjunction of the four qualification keys. The export sets all four to
    // `false`, so a disjunction would switch the rule off entirely and `this.` would stop being
    // removed — the exact behaviour this rule exists for. The old first disjunct
    // (`resharper_remove_this_qualifier`, always `true` in the export) was carrying the rule, and
    // removing a key must not change output.
    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Semantics, context.Options).Visit(context.Root);

    /// <summary>Whether the configured keys want a <c>this.</c> in front of this member.</summary>
    internal static bool WantsQualifier(in ArrangementOptions options, ISymbol symbol) =>
        symbol switch {
            IFieldSymbol => options.QualifyField,
            IPropertySymbol => options.QualifyProperty,
            IEventSymbol => options.QualifyEvent,
            IMethodSymbol => options.QualifyMethod,
            _ => false
        };

    /// <summary>
    ///     ⚠ Whether this member kind has a key at all. A local, a parameter and a type are not members
    ///     and never acquire a qualifier; without this the adding direction would try <c>this.</c> on
    ///     every identifier in the file and be stopped only by the symbol test, one binder call at a
    ///     time.
    /// </summary>
    internal static bool IsQualifiableMember(ISymbol symbol) =>
        symbol is IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol;

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model, ArrangementOptions options)
        : GuardedRewriter(guard) {
        readonly bool adds = options.QualifyField
            || options.QualifyProperty
            || options.QualifyMethod
            || options.QualifyEvent;

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (node.Expression is not ThisExpressionSyntax || !node.IsKind(SyntaxKind.SimpleMemberAccessExpression)) {
                return visited;
            }

            if (model.GetSymbolInfo(node).Symbol is not { } qualified) {
                return visited;
            }

            // ⚠ The key for *this member's kind* decides, not one switch for the whole file. A file
            // with `qualification_for_field = true` and `_for_property = false` keeps `this._field`
            // and loses `this.Property`, which is the oracle's own per-kind behaviour.
            if (WantsQualifier(options, qualified)) {
                return visited;
            }

            // ⚠ The precondition — the bare name, looked up at exactly this position, must find the
            // same symbol — lives on `GuardedRewriter`, because StaticQualifierRule needs the identical
            // test and two copies is how a fix to one of them misses the other.
            return BareNameResolvesTo(model, node, qualified) ? Unqualified(visited) : visited;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            if (!adds) {
                return visited;
            }

            // The right-hand side of a member access is already qualified, and a declaration's own
            // name is not a reference to it. `nameof(Member)` and an attribute argument read a name
            // rather than evaluate it, and `this.` inside `nameof` is not even legal there.
            if (node.Parent is MemberAccessExpressionSyntax { Name: var name } && name == node) {
                return visited;
            }

            if (node.Parent is MemberBindingExpressionSyntax
                or QualifiedNameSyntax
                or NameColonSyntax
                or NameEqualsSyntax) {
                return visited;
            }

            // ⚠ `ContainingSymbol is INamedTypeSymbol` rather than `ContainingType is not null`, and
            // the difference is a local function: it is an `IMethodSymbol` whose `ContainingType` is
            // the enclosing type, so the looser test writes `this.Local()` — which does not compile.
            if (model.GetSymbolInfo(node).Symbol is not { IsStatic: false } member
                || !IsQualifiableMember(member)
                || member.ContainingSymbol is not INamedTypeSymbol
                || !WantsQualifier(options, member)) {
                return visited;
            }

            // ⚠ `this` only exists in an instance body. A field initialiser, a static method, an
            // attribute argument and a constant context all bind the bare name perfectly well and
            // none of them may write `this`.
            if (!IsInInstanceBody(node)) {
                return visited;
            }

            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ThisExpression(),
                visited.WithoutTrivia()
            )
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        /// <summary>
        ///     Whether <c>this</c> is even a legal word at this position.
        /// </summary>
        /// <remarks>
        ///     ⚠ Three positions bind the bare name perfectly well and reject <c>this</c> outright, and
        ///     all three occur in ordinary code: a field or property <em>initialiser</em>
        ///     (<c>int b = a;</c> — CS0027), a <em>constructor initialiser</em>
        ///     (<c>: base(field)</c> — the object does not exist yet), and an <em>attribute</em>
        ///     argument. The enclosing symbol answers the first — an initialiser's enclosing symbol is
        ///     the field or property itself rather than a method — and the other two are syntax.
        ///     <para>
        ///         ⚠ The walk is over enclosing <em>symbols</em> rather than syntax because a lambda inside an
        ///         instance method is still an instance body while a lambda inside a static one is not, and
        ///         the syntax of the two is identical.
        ///     </para>
        /// </remarks>
        bool IsInInstanceBody(SyntaxNode node) {
            for (var current = node; current is not null; current = current.Parent) {
                if (current is ConstructorInitializerSyntax or AttributeSyntax) {
                    return false;
                }

                if (current is MemberDeclarationSyntax) {
                    break;
                }
            }

            for (var symbol = model.GetEnclosingSymbol(node.SpanStart);
                 symbol is not null;
                 symbol = symbol.ContainingSymbol) {
                if (symbol is ITypeSymbol or INamespaceSymbol) {
                    return false;
                }

                if (symbol is IMethodSymbol method) {
                    return !method.IsStatic && method.MethodKind != MethodKind.StaticConstructor;
                }

                // A field, property or event as the *enclosing* symbol means an initialiser.
                if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol) {
                    return false;
                }
            }

            return false;
        }
    }
}

/// <summary>
///     <c>{ { x; } }</c> ⇒ <c>{ x; }</c>, under <c>skala_braces_redundant</c>.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/06 § "Qualification and redundancy" resolves what looks like a contradiction in the
///     export — <c>csharp_prefer_braces = true</c> (Microsoft: always use braces) beside
///     <c>skala_braces_redundant = true</c> (ReSharper: remove braces that add nothing). They govern
///     different things: this rule removes a *nested block that is a statement of another block*, and
///     never the braces of an <c>if</c>, a <c>while</c> or a <c>using</c>. Reading it the other way
///     turns "always brace your ifs" into "unbrace them all".
///     <para>
///         ⚠ A block that declares anything is not redundant: hoisting its declarations into the parent
///         changes their scope, and can collide with a name the parent already has. That is the whole
///         precondition and it is checked syntactically, which is why this rule is in the free subset.
///     </para>
/// </remarks>
public sealed class RedundantBracesRule : ArrangementRule {
    public override string Id => ArrangeIds.RedundantBraces;

    public override bool NeedsSemantics => false;

    public override bool IsEnabled(in ArrangementOptions options) => options.BracesRedundant;

    public override SyntaxNode Apply(ArrangementContext context) => new Rewriter(context.Guard).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitBlock(BlockSyntax node) {
            var visited = (BlockSyntax)base.VisitBlock(node)!;
            var statements = new List<StatementSyntax>();
            var changed = false;

            foreach (var statement in visited.Statements) {
                if (statement is BlockSyntax inner && IsRedundant(inner)) {
                    // The inner block's own leading trivia belongs to its first statement now.
                    var first = true;
                    foreach (var lifted in inner.Statements) {
                        statements.Add(first ? lifted.WithLeadingTrivia(inner.GetLeadingTrivia()) : lifted);
                        first = false;
                    }

                    changed = true;
                    continue;
                }

                statements.Add(statement);
            }

            return changed ? visited.WithStatements(SyntaxFactory.List(statements)) : visited;
        }

        static bool IsRedundant(BlockSyntax block) {
            foreach (var statement in block.Statements) {
                // A declaration's scope is the block. Lifting it widens that scope.
                if (statement is LocalDeclarationStatementSyntax
                    or LocalFunctionStatementSyntax
                    or LabeledStatementSyntax) {
                    return false;
                }

                if (DeclaresIntoEnclosingScope(statement)) {
                    return false;
                }
            }

            // ⚠ A directive inside the braces may be what the braces are there for; a `#if` that
            // opens in one block and closes in another is exactly the shape ADR-003 refuses to move.
            foreach (var trivia in block.DescendantTrivia(descendIntoTrivia: true)) {
                if (trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Whether a statement introduces a name whose scope is the block holding it — the property
        ///     the three statement kinds above are only the <em>statement</em> half of.
        /// </summary>
        /// <remarks>
        ///     ⚠ #341. <c>var (a, b) = M();</c> and <c>M(out var n);</c> are
        ///     <c>ExpressionStatementSyntax</c>, and <c>if (o is string s)</c> is an
        ///     <c>IfStatementSyntax</c>; none of the three is a <c>LocalDeclarationStatementSyntax</c>, so
        ///     the kind list let all of them through and the block was lifted with its declarations. Under
        ///     <c>--arrange=syntactic</c> there is no compilation to re-bind against, so <c>SK9098</c> is
        ///     not there to catch it and the <c>CS0128</c> reaches disk. Measured on the issue's probe:
        ///     four <c>CS0128</c> and one <c>CS0165</c>, in a file Skala had just written.
        ///     <para>
        ///         ⚠ The walk stops at exactly two constructs, and stopping anywhere else would be a guess.
        ///         A <see cref="BlockSyntax" /> and the body of an
        ///         <see cref="AnonymousFunctionExpressionSyntax" /> are unconditionally declaration spaces:
        ///         nothing declared inside either can be seen outside it, in any C# version. Every other
        ///         scope-introducing construct — a switch section, a <c>catch</c> filter, the embedded
        ///         statement of an <c>if</c> — is *not* descended past, so a designation inside one is
        ///         reported as leaking when it does not. That over-rejects, on purpose: the cost is a
        ///         block left in place, and the cost of the opposite error is a file that no longer
        ///         compiles. <c>foreach (var (a, b) in xs) { … }</c> is the shape that pays it — the
        ///         designation is a child of the <c>foreach</c> itself, not of its body, so skipping the
        ///         body does not reach it and the enclosing block is refused.
        ///     </para>
        ///     <para>
        ///         ⚠ The issue proposed <c>statement.DescendantNodes()</c>, which over-rejects further —
        ///         it refuses a block whose only designation is inside a lambda, where the name provably
        ///         cannot escape. Two exclusions buy that case back without asserting anything about C#
        ///         that is not flatly true.
        ///     </para>
        /// </remarks>
        static bool DeclaresIntoEnclosingScope(SyntaxNode statement) {
            // A nested block scopes whatever it declares, so the statement itself being one settles it.
            if (statement is BlockSyntax) {
                return false;
            }

            var pending = new Stack<SyntaxNode>();
            pending.Push(statement);

            while (pending.Count > 0) {
                foreach (var child in pending.Pop().ChildNodes()) {
                    // `_` and `out _` name nothing; a parenthesized designation holds the singles.
                    if (child is SingleVariableDesignationSyntax) {
                        return true;
                    }

                    if (child is BlockSyntax or AnonymousFunctionExpressionSyntax) {
                        continue;
                    }

                    pending.Push(child);
                }
            }

            return false;
        }
    }
}

/// <summary>
///     <c>a + (b * c)</c> ⇒ <c>a + b * c</c>, under <c>skala_parentheses_redundancy_style</c>.
/// </summary>
/// <remarks>
///     ⚠ The single largest item in docs/plan/17's measurement: <c>ArrangeRedundantParentheses</c> fires
///     1 231 times on 900 Vixen files, more than any other inspection Skala did not perform.
///     <para>
///         ⚠ <b>Removal is proved, not computed.</b> The first version of this rule carried a precedence
///         table and was arithmetic-only because a table is exactly as trustworthy as its author. This one
///         asks the parser instead: remove the parentheses, print the enclosing expression, parse it back,
///         and keep the edit only when the re-parsed tree is structurally equivalent to the one that was
///         built. Parentheses are redundant *iff* deleting them re-parses to the same tree — that is the
///         definition, so checking it directly is both safer and broader than any table, and it is what lets
///         the rule cover casts, unary operators, invocations and nesting that the table version refused.
///     </para>
///     <para>
///         ⚠ Which parentheses Skala is *willing* to drop is a separate question from whether dropping them
///         is safe, and it is settled by the export rather than by the proof:
///         <c>dotnet_style_parentheses_in_arithmetic_binary_operators = never_if_unnecessary</c> and
///         <c>..._relational_binary_operators = never_if_unnecessary</c> against
///         <c>..._other_binary_operators = always_for_clarity</c>, with
///         <c>resharper_parentheses_non_obvious_operations</c> naming shift and the bitwise family. Measured
///         against <c>jb cleanupcode</c> 2025.2.6 rather than read: the oracle removes them around
///         arithmetic, relational, casts, unary operators, invocations and nested parentheses, and keeps
///         them around <c>&amp;&amp;</c>, <c>||</c>, <c>??</c>, shift and bitwise operands. The deciding
///         factor is the *inner* operation's kind, not the parent's — <c>(a &lt; b) &amp;&amp; (b &lt; c)</c>
///         loses its parentheses while <c>a || (b &amp;&amp; c)</c> keeps them.
///     </para>
/// </remarks>
public sealed class RedundantParenthesesRule : ArrangementRule {
    public override string Id => ArrangeIds.RedundantParentheses;

    public override bool NeedsSemantics => false;

    /// <summary>
    ///     ⚠ Both values of <c>skala_parentheses_redundancy_style</c> remove; they differ in
    ///     how much.
    /// </summary>
    /// <remarks>
    ///     ⚠ This read <c>== RemoveIfNotClarifiesPrecedence</c> and so did <em>nothing at all</em> at
    ///     <c>remove</c>, which is the stronger of the two values — ReSharper spells it "Always". The
    ///     export writes <c>remove_if_not_clarifies_precedence</c>, so the committed fixture could not
    ///     see it and the key's sweep row disagreed on sixteen lines of
    ///     <c>redundancy/parentheses.cs</c> with the sign of the whole rule inverted: the oracle removed
    ///     every parenthesis whose removal preserves the parse and Skala removed none.
    ///     <para>
    ///         ⚠ The key is the only switch. Removal was once gated behind <c>arrange --aggressive</c>
    ///         (SK-DIV-0014); the gate was lifted, the flag stayed and changed nothing, and #389 removed it.
    ///     </para>
    /// </remarks>
    public override bool IsEnabled(in ArrangementOptions options) =>
        options.ParenthesesRedundancy
        is ParenthesesRedundancyStyle.Remove
            or ParenthesesRedundancyStyle.RemoveIfNotClarifiesPrecedence;

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, ArrangementOptions options) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) {
            var visited = (ParenthesizedExpressionSyntax)base.VisitParenthesizedExpression(node)!;
            if (!ParenthesesRedundancy.MayRemove(node, options)) {
                return visited;
            }

            return ParenthesesRedundancy.RemovalPreservesParse(node)
                ? ParenthesesRedundancy.Strip(visited)
                : visited;
        }
    }
}

/// <summary>
///     The policy half and the proof half of <see cref="RedundantParenthesesRule" />, apart from the
///     rewriter so that both can be unit-tested on their own.
/// </summary>
public static class ParenthesesRedundancy {
    /// <summary>
    ///     Whether the export is willing to lose these parentheses at all — the policy question.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shift and the bitwise family are <c>resharper_parentheses_non_obvious_operations</c> and are
    ///     unconditional. The three <c>dotnet_style_parentheses_in_*_binary_operators</c> keys decide the
    ///     rest, and they decide it on the <em>pair</em>: a parenthesised binary expression keeps its
    ///     parentheses only when its parent is also a binary expression of the same precedence kind and
    ///     that kind's key says <c>always_for_clarity</c>. Every other expression — an assignment, a
    ///     conditional, a lambda, a query, a <c>switch</c> — is left to the proof, because that is what
    ///     the oracle does (#392); the guesses that kept them are recorded in the body.
    ///     <para>
    ///         ⚠ The pair test is measured rather than inferred, and the first version of this rule did not
    ///         have it: keying on the inner expression alone keeps <c>return (a &amp;&amp; b);</c> and
    ///         <c>M((a &amp;&amp; b))</c>, and the oracle removes both — the export's
    ///         <c>other_binary_operators = always_for_clarity</c> only ever holds an operand of another
    ///         <c>&amp;&amp;</c>, <c>||</c> or <c>??</c>. Ten cases were probed at all four interesting
    ///         combinations of the three keys, and <c>(a + b) &gt; c</c> — arithmetic inside relational —
    ///         is removed at every one of them.
    ///     </para>
    /// </remarks>
    public static bool MayRemove(ParenthesizedExpressionSyntax node, in ArrangementOptions options) {
        // ⚠ An interpolation hole used to be declined whole, because a `:` or a `,` there is a format
        // or an alignment clause and re-parsing the expression alone answered a question that was not
        // asked. The question is now asked properly — the proof re-parses the interpolated string the
        // hole sits in (`Outermost`) — and the oracle removes the parentheses in every hole whose parse
        // allows it (#392): `$"{(a + b):D2}"`, `$"{(x switch { … }),5}"`. `$"{(b ? 1 : 2)}"` keeps
        // them because the `:` would become a format clause, and the re-parse is what says so.

        // ⚠ `(A)(b)` is a cast when `A` names a type and an invocation when it does not, and the
        // parser cannot tell without semantics. This rule has none, so it declines the whole shape.
        if (node.Parent is CastExpressionSyntax) {
            return false;
        }

        // ⚠ A `(` and a `)` either side of an `#if` do not necessarily both survive into the same
        // compilation, so a directive in the parentheses' own trivia declines them. A *comment* there
        // is carried out by `Strip` instead — it used to be dropped with the parentheses, which made
        // `return (/* why */ a + b);` lose its sentence.
        if (HasDirective(InsideLeft(node)) || HasDirective(InsideRight(node))) {
            return false;
        }

        // ⚠ The operand of a `throw` *expression* keeps its parentheses around a `switch` or a binary
        // operator, which the parse alone would not say: measured under the cleanup profile, the oracle
        // keeps `=> throw (v switch { … })` and `=> throw (e ?? new Exception())` and removes
        // `=> throw (E())`, and a `throw` *statement* loses them around all three (#392, SK-DIV-0145).
        if (node.Parent is ThrowExpressionSyntax
            && node.Expression is BinaryExpressionSyntax or SwitchExpressionSyntax) {
            return false;
        }

        // ⚠ `remove` is ReSharper's "Always", and it outranks every policy below: the three
        // `dotnet_style_parentheses_in_*` keys *and* `resharper_parentheses_non_obvious_operations`.
        // Measured on `redundancy/parentheses.cs` under the cleanup profile — at `remove` the oracle
        // strips `a || (b && c)`, `a | (b & c)`, `a & (b + 1)`, `a << (b + 1)` and
        // `(value >> offset) & ((1 << take) - 1)`, all of which it keeps at the export's
        // `remove_if_not_clarifies_precedence`, and it strips nothing whose removal changes the parse:
        // `a - (b - c)`, `a / (b / c)`, `(a << b) + c`, `(x = a) + 1` and `(a ? b : c) + 1` all hold at
        // both values. So `remove` is exactly this rule with the policy layer off and the proof left
        // standing.
        var always = options.ParenthesesRedundancy == ParenthesesRedundancyStyle.Remove;

        // ⚠ The *enclosing* operation matters too, and this was measured after being got wrong.
        // `resharper_parentheses_non_obvious_operations = shift, bitwise_*` does not say "keep the
        // parentheses that wrap a shift"; it says "clarify the precedence *of* these operations",
        // which means keeping the parentheses around their operands. `a & (b + 1)` and
        // `a << (b + 1)` both keep theirs even though the inner expression is plain arithmetic. The
        // first version of this rule keyed on the inner expression alone, agreed with the oracle on
        // every case in the fixture, and stripped these two anyway — found by reading what it did to
        // Vixen's `BitReader`, not by a test.
        //
        // ⚠ And it is about *operator* operands only, which this read as "every operand" until #392
        // asked. Measured at the export's value: the oracle removes `a & (-b)`, `a << (-b)`,
        // `a & (M(a))`, `b & (a.Length)`, `b & ((int)o)`, `b & (!c)`, `b << (v switch { … })`,
        // `b & (o is string)`, `b & (o is string s)` and `(b & (o as bool?))`, and keeps
        // `b & (x == 1)` beside `a & (b + 1)`. `is` and `as` are binary nodes in Roslyn's tree and
        // relational in its precedence table, and the oracle does not count them as operations here.
        if (!always
            && node.Parent is BinaryExpressionSyntax { RawKind: var parentKind }
            && IsNonObvious((SyntaxKind)parentKind)
            && node.Expression is BinaryExpressionSyntax inner
            && !inner.IsKind(SyntaxKind.IsExpression)
            && !inner.IsKind(SyntaxKind.AsExpression)) {
            return false;
        }

        return node.Expression switch {
            // The always_for_clarity families, and the non-obvious operations.
            //
            // ⚠ `a ?? (b ?? c)` is the one shape `remove` does *not* reach, and it is measured rather
            // than reasoned: asked at both values on a probe carrying `a ?? (b ?? c)`,
            // `a || (b || c)`, `a && (b && c)`, `a + (b + c)` and `a ? b : (c ? d : e)`, the oracle
            // removes all of the others at both values and keeps this one at both. Right-associativity
            // is not the reason — the conditional operator is right-associative too and loses its
            // parentheses. At the export's value `IsKept` already keeps it, so this changes nothing
            // there; it is the `remove` bypass that would otherwise take it.
            BinaryExpressionSyntax binary =>
                !IsCoalesceNesting(binary.Kind(), node.Parent)
                && (always || !IsKept(binary.Kind(), node.Parent, options)),

            // ⚠ There used to be two more arms here, and both were guesses the oracle refutes (#392).
            //
            // "`(x = 1)` and `(a ? b : c)` inside a larger expression are doing work the reader is
            // being shown" kept every assignment and conditional. The oracle removes them wherever the
            // parse allows — `return (x = a);`, `x = (y = a);`, `return (b ? 1 : 2);`,
            // `a ? (b ? 1 : 2) : 3`, `a[(b ? 0 : 1)]` — and keeps exactly the ones the parse needs,
            // `(x = a) + 1` and `(a ? b : c) + 1`, which the proof keeps by itself.
            //
            // "A lambda, a query or a `switch` expression inside parentheses is a readability decision
            // the oracle also leaves alone" kept all three. Asked in some thirty positions under the
            // cleanup profile, the oracle removes them in a `return`, a `var` initializer, an arrow
            // body, an argument, a lambda body, a `?:` branch, an interpolation hole, a `when` clause,
            // a `with` initializer, a collection element and a binary or `is`/`as` operand, and keeps
            // them only where the parse does — before `.`, `?.`, `(`, `[`, `!` and `..`, after a unary
            // operator, `await` or a cast, and as the right operand of `??` (`f ?? () => 1` and
            // `b ?? from x in a select x` do not parse). The two places the parse does not explain are
            // the `switch` arm below and the `throw` expression above.
            //
            // ⚠ `IsPatternExpressionSyntax` was once on that list for the same reason and is not:
            // `(o is string s) && …` looks like a case where the parentheses earn their keep, and the
            // oracle removes them.
            //
            // ⚠ A `switch` expression as the receiver of `with` or the subject of another `switch`
            // keeps its parentheses although Roslyn parses `v switch { … } with { … }` and
            // `v switch { … } switch { … }` to the same tree without them: the oracle keeps both
            // (SK-DIV-0145).
            // (An arm's body is the child of a `SwitchExpressionArmSyntax`, so a `switch` parent here
            // is always the subject position.)
            SwitchExpressionSyntax => node.Parent is not (WithExpressionSyntax or SwitchExpressionSyntax),

            _ => true
        };
    }

    static bool HasDirective(SyntaxTriviaList trivia) =>
        trivia.Any(static t => t.IsDirective || t.IsKind(SyntaxKind.DisabledTextTrivia));

    static bool HasComment(SyntaxTriviaList trivia) =>
        trivia.Any(static t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
            || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
            || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        );

    /// <summary>
    ///     ⚠ The proof: printing the expression without its parentheses and parsing it back must give
    ///     structurally the same tree.
    /// </summary>
    /// <remarks>
    ///     ⚠ The comparison is against the tree that was *built*, not against the original — the
    ///     question is "does the text I am about to write still mean this", and only a re-parse answers
    ///     it. <see cref="SyntaxNode.IsEquivalentTo" /> with <c>topLevel: false</c> compares structure and
    ///     tokens and ignores trivia, which is exactly the grain wanted: whitespace is the formatter's
    ///     and a precedence change is never invisible to it.
    ///     <para>
    ///         The subject is the outermost enclosing expression rather than the parent, because precedence
    ///         reaches further than one node: in <c>a * (b + c) * d</c> the parent alone would not show that
    ///         the second <c>*</c> also binds the operand.
    ///     </para>
    /// </remarks>
    public static bool RemovalPreservesParse(ParenthesizedExpressionSyntax node) {
        var outer = Outermost(node);
        var expected = outer.ReplaceNode(node, Strip(node));
        var printed = expected.ToFullString();

        // A hard cap: the proof is a parse of the enclosing expression, and a generated file can
        // carry a single expression of a hundred thousand characters. Re-parsing that once per
        // candidate is the one shape of this rule that is quadratic.
        if (printed.Length > MaxProofLength) {
            return false;
        }

        var reparsed = SyntaxFactory.ParseExpression(printed);
        return !reparsed.ContainsDiagnostics
            && reparsed.IsEquivalentTo(expected, false);
    }

    const int MaxProofLength = 8192;

    /// <summary>
    ///     The expression without its parentheses, keeping every comment that sat inside them.
    /// </summary>
    /// <remarks>
    ///     ⚠ The trivia inside the parentheses — after <c>(</c>, before <c>)</c> — used to be dropped
    ///     with them, so <c>return (/* why */ a + b);</c> arranged to <c>return a + b;</c> and the
    ///     sentence was gone. The oracle keeps it (<c>/* why */a + b</c>). Only a comment carries the
    ///     inner trivia across; plain whitespace there is the parentheses' own and goes with them.
    /// </remarks>
    public static ExpressionSyntax Strip(ParenthesizedExpressionSyntax node) {
        var left = InsideLeft(node);
        var right = InsideRight(node);
        return node.Expression
            .WithLeadingTrivia(HasComment(left) ? node.GetLeadingTrivia().AddRange(left) : node.GetLeadingTrivia())
            .WithTrailingTrivia(
                HasComment(right) ? right.AddRange(node.GetTrailingTrivia()) : node.GetTrailingTrivia()
            );
    }

    /// <summary>The trivia between <c>(</c> and the expression's first token.</summary>
    static SyntaxTriviaList InsideLeft(ParenthesizedExpressionSyntax node) =>
        node.OpenParenToken.TrailingTrivia.AddRange(node.Expression.GetLeadingTrivia());

    /// <summary>The trivia between the expression's last token and <c>)</c>.</summary>
    static SyntaxTriviaList InsideRight(ParenthesizedExpressionSyntax node) =>
        node.Expression.GetTrailingTrivia().AddRange(node.CloseParenToken.LeadingTrivia);

    /// <summary>The largest enclosing expression, which is what the parser's precedence spans.</summary>
    /// <remarks>
    ///     ⚠ It climbs through the nodes that live <em>inside</em> an expression without being one —
    ///     an argument, an interpolation hole, a <c>switch</c> arm, a query clause, a collection
    ///     element — because a removal can change the parse beyond them, and stopping there measured the
    ///     wrong thing (#392). <c>F((a &lt; b), c &gt; (x = 1))</c> is two comparisons; with the first
    ///     pair gone it is <c>F(a &lt; b, c &gt; (x = 1))</c>, a generic invocation, and a proof that
    ///     re-parsed <c>a &lt; b</c> alone said the removal was safe. The oracle keeps that pair.
    /// </remarks>
    static ExpressionSyntax Outermost(ExpressionSyntax node) {
        var current = node;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent) {
            // ⚠ `ref x` does not parse as a whole expression on its own, so climbing into one turns
            // every proof beneath it into a parse error and a refusal. Found by the arrangement
            // differential the first time the climb reached through `[…]`: Vixen's
            // `ref field.Cells[x + (z * field.Width)]` kept parentheses the oracle removes.
            if (ancestor is RefExpressionSyntax) {
                break;
            }

            if (ancestor is ExpressionSyntax expression) {
                current = expression;
            } else if (!LivesInsideAnExpression(ancestor)) {
                break;
            }
        }

        return current;
    }

    static bool LivesInsideAnExpression(SyntaxNode node) =>
        node is ArgumentSyntax
            or BaseArgumentListSyntax
            or InterpolationSyntax
            or InterpolationAlignmentClauseSyntax
            or InterpolationFormatClauseSyntax
            or SwitchExpressionArmSyntax
            or WhenClauseSyntax
            or QueryBodySyntax
            or QueryClauseSyntax
            or SelectOrGroupClauseSyntax
            or OrderingSyntax
            or JoinIntoClauseSyntax
            or QueryContinuationSyntax
            or CollectionElementSyntax
            or AnonymousObjectMemberDeclaratorSyntax
            or NameEqualsSyntax
            or NameColonSyntax;

    /// <summary>
    ///     The operations <c>resharper_parentheses_non_obvious_operations</c> names: an operand of one
    ///     of these keeps its parentheses whatever the operand is.
    /// </summary>
    static bool IsNonObvious(SyntaxKind kind) =>
        kind is SyntaxKind.LeftShiftExpression
            or SyntaxKind.RightShiftExpression
            or SyntaxKind.UnsignedRightShiftExpression
            or SyntaxKind.BitwiseAndExpression
            or SyntaxKind.BitwiseOrExpression
            or SyntaxKind.ExclusiveOrExpression;

    /// <summary>
    ///     A <c>??</c> that is the operand of another <c>??</c>, which keeps its parentheses at both
    ///     values of <c>skala_parentheses_redundancy_style</c>.
    /// </summary>
    static bool IsCoalesceNesting(SyntaxKind kind, SyntaxNode? parent) =>
        kind == SyntaxKind.CoalesceExpression
        && parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression };

    /// <summary>
    ///     The binary families whose parentheses the configuration keeps in this position.
    /// </summary>
    static bool IsKept(SyntaxKind kind, SyntaxNode? parent, in ArrangementOptions options) {
        // ⚠ `resharper_parentheses_non_obvious_operations`, and it is unconditional: an operand of a
        // shift or a bitwise operator keeps its parentheses wherever it stands. Measured to survive
        // all eight combinations of the three Roslyn keys.
        if (IsNonObvious(kind)) {
            return true;
        }

        // The three keys only speak about a binary operand of a binary operator of the same kind.
        // `return (a + b);` and `(a + b) > c` have no key and lose their parentheses.
        if (parent is not BinaryExpressionSyntax binaryParent
            || PrecedenceKindOf(kind) is not { } inner
            || PrecedenceKindOf(binaryParent.Kind()) != inner) {
            return false;
        }

        return Preference(inner, options) == ParenthesesPreference.AlwaysForClarity;
    }

    static ParenthesesPreference Preference(PrecedenceKind kind, in ArrangementOptions options) =>
        kind switch {
            PrecedenceKind.Arithmetic => options.ParenthesesInArithmetic,
            PrecedenceKind.Relational => options.ParenthesesInRelational,
            _ => options.ParenthesesInOther
        };

    /// <summary>
    ///     Roslyn's three precedence kinds, which are what the three keys are named after.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shift and the bitwise family belong to <see cref="PrecedenceKind.Arithmetic" /> in Roslyn's
    ///     own grouping, and they are deliberately absent here: on this repository's configuration they
    ///     are answered earlier and unconditionally by <see cref="IsNonObvious" />, and folding them into
    ///     the arithmetic key would make <c>a + (b &lt;&lt; c)</c> lose its parentheses at
    ///     <c>never_if_unnecessary</c> where the oracle keeps them.
    /// </remarks>
    static PrecedenceKind? PrecedenceKindOf(SyntaxKind kind) =>
        kind switch {
            SyntaxKind.MultiplyExpression
                or SyntaxKind.DivideExpression
                or SyntaxKind.ModuloExpression
                or SyntaxKind.AddExpression
                or SyntaxKind.SubtractExpression => PrecedenceKind.Arithmetic,
            SyntaxKind.LessThanExpression
                or SyntaxKind.GreaterThanExpression
                or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanOrEqualExpression
                or SyntaxKind.EqualsExpression
                or SyntaxKind.NotEqualsExpression
                or SyntaxKind.IsExpression
                or SyntaxKind.AsExpression => PrecedenceKind.Relational,
            SyntaxKind.LogicalAndExpression
                or SyntaxKind.LogicalOrExpression
                or SyntaxKind.CoalesceExpression => PrecedenceKind.Other,
            _ => null
        };

    enum PrecedenceKind {
        Arithmetic,
        Relational,
        Other
    }
}
