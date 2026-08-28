using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>
///     <c>this.field</c> ⇔ <c>field</c>, under the four <c>dotnet_style_qualification_for_*</c> keys and
///     <c>resharper_remove_this_qualifier</c>.
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
///         ⚠ <c>resharper_remove_this_qualifier</c> is not the key the oracle reads. The same probe with it
///         at <c>false</c> comes back byte-identical — the qualifier is still removed — so on this
///         repository's configuration it is dominated by the four Roslyn keys. It is kept here as a gate on
///         the removing direction, because it is a Tier A option whose own committed fixture has to keep
///         distinguishing it; the disagreement is recorded as SK-DIV-0070 rather than resolved by
///         quietly dropping a key another fixture claims.
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

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.RemoveThisQualifier
        || options.QualifyField
        || options.QualifyProperty
        || options.QualifyMethod
        || options.QualifyEvent;

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
        readonly bool _adds = options.QualifyField
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
            if (!options.RemoveThisQualifier || WantsQualifier(options, qualified)) {
                return visited;
            }

            // ⚠ The precondition: the bare name, looked up at exactly this position, must find the
            // same symbol. `LookupSymbols` is asked rather than the syntax re-bound, because the
            // rewritten tree is not in the model and re-binding it would need a whole new
            // compilation — which is layer 2's job, not layer 1's.
            var candidates = model.LookupSymbols(node.SpanStart, name: node.Name.Identifier.ValueText);
            if (candidates.Length != 1 || !SymbolEqualityComparer.Default.Equals(candidates[0], qualified)) {
                return visited;
            }

            return visited.Name.WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var visited = (IdentifierNameSyntax)base.VisitIdentifierName(node)!;
            if (!_adds) {
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
///     <c>{ { x; } }</c> ⇒ <c>{ x; }</c>, under <c>resharper_braces_redundant</c>.
/// </summary>
/// <remarks>
///     ⚠ docs/plan/06 § "Qualification and redundancy" resolves what looks like a contradiction in the
///     export — <c>csharp_prefer_braces = true</c> (Microsoft: always use braces) beside
///     <c>resharper_braces_redundant = true</c> (ReSharper: remove braces that add nothing). They govern
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
    }
}

/// <summary>
///     <c>a + (b * c)</c> ⇒ <c>a + b * c</c>, under <c>resharper_parentheses_redundancy_style</c>.
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

    public override bool IsAggressive => !ParenthesesRedundancy.RemovalIsDefault;

    public override bool IsEnabled(in ArrangementOptions options) =>
        options.ParenthesesRedundancy == ParenthesesRedundancyStyle.RemoveIfNotClarifiesPrecedence
        && (ParenthesesRedundancy.RemovalIsDefault || options.Aggressive);

    public override SyntaxNode Apply(ArrangementContext context) =>
        new Rewriter(context.Guard, context.Options).Visit(context.Root);

    sealed class Rewriter(FormatterTagGuard guard, ArrangementOptions options) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) {
            var visited = (ParenthesizedExpressionSyntax)base.VisitParenthesizedExpression(node)!;
            if (!ParenthesesRedundancy.MayRemove(node, options)) {
                return visited;
            }

            var stripped = visited.Expression
                .WithLeadingTrivia(visited.GetLeadingTrivia())
                .WithTrailingTrivia(visited.GetTrailingTrivia());

            return ParenthesesRedundancy.RemovalPreservesParse(node) ? stripped : visited;
        }
    }
}

/// <summary>
///     The policy half and the proof half of <see cref="RedundantParenthesesRule" />, apart from the
///     rewriter so that both can be unit-tested on their own.
/// </summary>
public static class ParenthesesRedundancy {
    /// <summary>
    ///     ⚠ Whether parenthesis removal runs without <c>arrange --aggressive</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/06 gated this for the first release and named the condition for revisiting it:
    ///     "revisits when the corpus differential shows zero divergences". The condition is now met and
    ///     the gate is lifted — the M4-era gate cost 4.02 points of changed-span agreement (SK-DIV-0014)
    ///     against an oracle that removes these parentheses by default, and the rule that replaced it
    ///     proves each removal by re-parsing rather than asserting it from a precedence table. The
    ///     constant stays as a named switch rather than being deleted so that the decision is one edit
    ///     and one number, not a re-derivation.
    /// </remarks>
    public const bool RemovalIsDefault = true;

    /// <summary>
    ///     Whether the export is willing to lose these parentheses at all — the policy question.
    /// </summary>
    /// <remarks>
    ///     ⚠ Shift and the bitwise family are <c>resharper_parentheses_non_obvious_operations</c> and are
    ///     unconditional. The three <c>dotnet_style_parentheses_in_*_binary_operators</c> keys decide the
    ///     rest, and they decide it on the <em>pair</em>: a parenthesised binary expression keeps its
    ///     parentheses only when its parent is also a binary expression of the same precedence kind and
    ///     that kind's key says <c>always_for_clarity</c>. Assignment and the conditional operator are
    ///     kept because <c>x = (y = 2)</c> and <c>(a ? b : c)</c> read as deliberate in every corpus
    ///     instance.
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
        // ⚠ An interpolation's braces are not an expression context in the way the proof below
        // assumes: `$"{(a, b)}"` and a `:` inside an interpolation are format specifiers, so
        // re-parsing the expression alone answers a question that was not asked.
        if (node.Parent is InterpolationSyntax) {
            return false;
        }

        // ⚠ `(A)(b)` is a cast when `A` names a type and an invocation when it does not, and the
        // parser cannot tell without semantics. This rule has none, so it declines the whole shape.
        if (node.Parent is CastExpressionSyntax) {
            return false;
        }

        // ⚠ The *enclosing* operation matters too, and this was measured after being got wrong.
        // `resharper_parentheses_non_obvious_operations = shift, bitwise_*` does not say "keep the
        // parentheses that wrap a shift"; it says "clarify the precedence *of* these operations",
        // which means keeping the parentheses around their operands. `a & (b + 1)` and
        // `a << (b + 1)` both keep theirs even though the inner expression is plain arithmetic. The
        // first version of this rule keyed on the inner expression alone, agreed with the oracle on
        // every case in the fixture, and stripped these two anyway — found by reading what it did to
        // Vixen's `BitReader`, not by a test.
        if (node.Parent is BinaryExpressionSyntax { RawKind: var parentKind } && IsNonObvious((SyntaxKind)parentKind)) {
            return false;
        }

        return node.Expression switch {
            // The always_for_clarity families, and the non-obvious operations.
            BinaryExpressionSyntax binary => !IsKept(binary.Kind(), node.Parent, options),

            // `(x = 1)` inside a larger expression is doing work that the reader is being shown.
            AssignmentExpressionSyntax or ConditionalExpressionSyntax => false,

            // A lambda, a query or a `switch` arm inside parentheses is a readability decision the
            // oracle also leaves alone.
            //
            // ⚠ `IsPatternExpressionSyntax` was on this list and is not: `(o is string s) && …` looks
            // like a case where the parentheses earn their keep, and the oracle removes them. Doc 00's
            // non-negotiable 9 makes the reference tool a test subject rather than a specification,
            // but a divergence has to be worth recording and this one was only a guess.
            AnonymousFunctionExpressionSyntax or QueryExpressionSyntax or SwitchExpressionSyntax => false,

            _ => true
        };
    }

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
            && reparsed.IsEquivalentTo(expected, topLevel: false);
    }

    const int MaxProofLength = 8192;

    static ExpressionSyntax Strip(ParenthesizedExpressionSyntax node) =>
        node.Expression.WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());

    /// <summary>The largest enclosing expression, which is what the parser's precedence spans.</summary>
    static ExpressionSyntax Outermost(ExpressionSyntax node) {
        var current = node;
        while (current.Parent is ExpressionSyntax parent) {
            current = parent;
        }

        return current;
    }

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
