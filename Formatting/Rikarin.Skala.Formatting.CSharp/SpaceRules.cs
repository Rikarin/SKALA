using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     One inter-token gap resolved to Required or Forbidden.
/// </summary>
/// <remarks>
///     docs/plan/05 § "Spaces": ninety keys, each of which resolves one gap.
///     <c>
/// extra_spaces =
///  remove_all
///     </c> is the global backstop — any run of spaces not required by a rule collapses to
///     one or to none — which is why this function is total: there is no "leave it alone" answer.
///     <para>
///         ⚠ <see cref="MustSeparate" /> overrides everything. A Forbidden gap between two tokens that would
///         lex as one produces a corrupted file. The safety net would catch it and abandon the file, which
///         is correct behaviour for a bug but is still a bug; this is the place not to make it.
///     </para>
/// </remarks>
public static class SpaceRules {
    public static SpaceKind Decide(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) {
        if (MustSeparate(prev, next)) {
            return SpaceKind.Required;
        }

        return Ungoverned(prev, next)
            ? SpaceKind.Preserve
            : Required(prev, next, o) ? SpaceKind.Required : SpaceKind.Forbidden;
    }

    /// <summary>
    ///     True exactly when <see cref="Decide" /> answers <see cref="SpaceKind.Preserve" /> for the gap:
    ///     no key governs it and the formatter writes back the author's own bit — one space if any
    ///     horizontal space was written, none otherwise.
    /// </summary>
    /// <remarks>
    ///     ⚠ Exposed for the fuzzer. Its absorption property says that whitespace-only mutation never
    ///     changes the output, and that is false as stated over precisely these gaps, since the oracle
    ///     keeps what the author wrote in them (#373, docs/plan/12 § "Where the properties are not what
    ///     this document said"). Its whitespace mutations skip every gap this predicate answers, and they
    ///     ask <em>this</em> predicate rather than carrying their own list, so the next gap
    ///     <see cref="Ungoverned" /> learns is excluded the day it lands. Excluding by hand — any gap
    ///     touching a range operator — was how #376 happened: the positional-pattern gap joined
    ///     <see cref="Ungoverned" /> and the fuzzer read the oracle's behaviour as a violation.
    ///     Independent of options, as <see cref="Ungoverned" /> is.
    /// </remarks>
    public static bool Preserves(SyntaxToken prev, SyntaxToken next) =>
        !MustSeparate(prev, next) && Ungoverned(prev, next);

    /// <summary>
    ///     The gap between a block comment and the token after it, where <paramref name="prev" /> is the
    ///     token in front of the comment — on the same line — and <paramref name="next" /> the one behind.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not "always one space", which is what Skala answered until #410 (SK-DIV-0174). Asked directly — every gap
    ///     written closed and spaced, and again with 37 keys flipped in two sets — the oracle answers this
    ///     gap one of three ways, and which one is decided by who owns the gap when the comment is not
    ///     there.
    ///     <list type="number">
    ///         <item>
    ///             The next token's own rule, read as though the comment were absent, wherever that rule
    ///             is the next token's: <c>a /* f */,</c>, <c>b /* f */)</c>, <c>1 /* f */;</c>,
    ///             <c>a /* f */.B()</c>, <c>M /* f */(</c>, <c>G /* f */&lt;int&gt;</c>, a closing
    ///             <c>]</c>, <c>&gt;</c> or <c>}</c>, any colon, a ternary or nullable <c>?</c>, a postfix
    ///             <c>++</c>, a binary or assignment operator, <c>=&gt;</c> and an opening brace. Every one
    ///             of them moved with its key (<c>space_before_comma = true</c> gives <c>a /* f */ ,</c>;
    ///             <c>space_around_binary_operators = none</c> gives <c>f /* f */+ 1</c>). ⚠ An empty pair
    ///             is not empty once it holds a comment: <c>base( /* f */)</c> reads
    ///             <c>space_within_method_call_parentheses</c>, not its empty twin, and
    ///             <c>new int[] { /* f */ }</c> the array-initializer key, not
    ///             <c>space_within_empty_braces</c>.
    ///         </item>
    ///         <item>
    ///             The author's bit where the rule belongs to the token <em>before</em> the comment:
    ///             after <c>(</c>, <c>[</c>, <c>,</c>, an initializer's <c>{</c>, a binary, assignment or
    ///             prefix operator, <c>=&gt;</c>, a ternary's <c>?</c> and <c>:</c>, <c>.</c>, a cast,
    ///             <c>new</c>, a <c>for</c>'s <c>;</c>, a base list's or a constraint's <c>:</c>, and the
    ///             keywords <c>return</c>, <c>throw</c>, <c>await</c>, <c>in</c>, <c>case</c>,
    ///             <c>where</c>, <c>not</c>. <c>M( /* f */a)</c> and <c>M( /* f */ a)</c> both come back
    ///             as written, and stay so with the key that governs the gap without the comment flipped
    ///             (<c>space_after_comma = false</c> keeps <c>, /* f */ b</c>;
    ///             <c>space_within_parentheses = true</c> keeps <c>( /* f */a</c>). A parameter's name is
    ///             the same: <c>int /* f */x</c>.
    ///         </item>
    ///         <item>
    ///             One space everywhere else that was measured, whatever the author wrote: after
    ///             <c>is</c>, <c>as</c>, <c>out</c>, <c>ref</c> and a modifier, between a member's type
    ///             and its name, after a named argument's colon and a case label's, before an accessor,
    ///             and in front of a collection expression's <c>[</c> even after an <c>=</c>. It is also
    ///             what an unmeasured gap gets, because it is what every gap got before.
    ///         </item>
    ///     </list>
    ///     Every key in the first class is read on the true neighbours, so this answers a different
    ///     question from <see cref="Decide" /> rather than overriding it — the comment always separates
    ///     the two tokens, so <see cref="MustSeparate" /> has nothing to say.
    /// </remarks>
    public static SpaceKind AfterBlockComment(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) {
        if (Ungoverned(prev, next)) {
            return SpaceKind.Preserve;
        }

        if (OwnsTheGapBeforeIt(next)) {
            return RequiredAcrossAComment(prev, next, o) ? SpaceKind.Required : SpaceKind.Forbidden;
        }

        if (next.IsKind(SyntaxKind.OpenBracketToken) && next.Parent is CollectionExpressionSyntax) {
            return SpaceKind.Required;
        }

        return KeepsTheAuthorsGapAfterAComment(prev, next) ? SpaceKind.Preserve : SpaceKind.Required;
    }

    /// <summary>
    ///     The gap between <paramref name="prev" /> and a block comment after it, when a key that governs
    ///     the gap behind <paramref name="prev" /> decides it; null when
    ///     <c>skala_space_before_trailing_comment</c> does, as it does for every other token.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two keys reach across the comment, and only two, and they reach in both directions. At
    ///     <c>space_around_assignment_op = false</c> the oracle writes <c>f=/* f */ 2</c> and
    ///     <c>f +=/* f */1</c>, and at <c>space_around_lambda_arrow = false</c> <c>x =&gt;/* f */x</c>, for a
    ///     lambda and an expression body alike; at <c>skala_space_before_trailing_comment = false</c> it
    ///     still writes <c>f = /* f */2</c> and <c>x =&gt; /* f */x</c>, where every other token loses the
    ///     space. <c>space_around_binary_operators = none</c> leaves <c>f + /* f */1</c> alone, and a switch
    ///     arm's arrow keeps its space, so neither is here (#410).
    /// </remarks>
    public static bool? BeforeBlockComment(SyntaxToken prev, in PhaseOneOptions o) {
        if (IsAssignmentOperator(prev)) {
            return o.SpaceAroundAssignmentOp;
        }

        return prev.IsKind(SyntaxKind.EqualsGreaterThanToken) && prev.Parent is not SwitchExpressionArmSyntax
            ? o.SpaceAroundLambdaArrow
            : null;
    }

    /// <summary>
    ///     The tokens whose left gap is theirs: the first class in <see cref="AfterBlockComment" />.
    /// </summary>
    static bool OwnsTheGapBeforeIt(SyntaxToken next) =>
        next.Kind() switch {
            SyntaxKind.CommaToken
                or SyntaxKind.SemicolonToken
                or SyntaxKind.CloseParenToken
                or SyntaxKind.CloseBracketToken
                or SyntaxKind.CloseBraceToken
                or SyntaxKind.OpenBraceToken
                or SyntaxKind.ColonToken
                or SyntaxKind.EqualsGreaterThanToken => true,
            SyntaxKind.QuestionToken => true,
            // ⚠ Only a parenthesis that hangs off what precedes it. `f + /* f */(f)`, a cast and a
            // lambda's parameters start an operand, and the oracle keeps the author's gap in front of
            // them as it does in front of any other operand.
            SyntaxKind.OpenParenToken => next.Parent
                is ArgumentListSyntax
                    or AttributeArgumentListSyntax
                    or ParameterListSyntax { Parent: not ParenthesizedLambdaExpressionSyntax }
                    or IfStatementSyntax
                    or WhileStatementSyntax
                    or DoStatementSyntax
                    or ForStatementSyntax
                    or CommonForEachStatementSyntax
                    or SwitchStatementSyntax
                    or CatchDeclarationSyntax
                    or CatchFilterClauseSyntax
                    or LockStatementSyntax
                    or UsingStatementSyntax
                    or FixedStatementSyntax
                    or CheckedExpressionSyntax
                    or DefaultExpressionSyntax
                    or SizeOfExpressionSyntax
                    or TypeOfExpressionSyntax,
            SyntaxKind.OpenBracketToken => next.Parent is BracketedArgumentListSyntax or ArrayRankSpecifierSyntax,
            _ => IsMemberAccessPunctuation(next)
                || IsTypeAngle(next)
                || IsPostfixOperator(next)
                || IsBinaryOperator(next)
                || IsAssignmentOperator(next)
                || next.IsKind(SyntaxKind.IsKeyword)
                || next.IsKind(SyntaxKind.SwitchKeyword)
                && next.Parent is SwitchExpressionSyntax
        };

    /// <summary>
    ///     <see cref="Required" /> on the true neighbours, with a pair the comment sits inside read as the
    ///     non-empty pair it now is.
    /// </summary>
    static bool RequiredAcrossAComment(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) =>
        (prev.Kind(), next.Kind()) switch {
            (SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken) => WithinParentheses(next.Parent, false, o),
            (SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken) => WithinBrackets(next.Parent, false, o),
            (SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken) => WithinBraces(next.Parent, default, o),
            _ => Required(prev, next, o)
        };

    /// <summary>
    ///     The second class in <see cref="AfterBlockComment" />: the token in front of the comment owns
    ///     the gap, and the oracle has no rule for a comment standing in it.
    /// </summary>
    static bool KeepsTheAuthorsGapAfterAComment(SyntaxToken prev, SyntaxToken next) {
        // A parameter's name, and three contextual keywords that follow an operand: `x /* f */in a`,
        // `int i /* f */when …` and `r /* f */with { … }` all come back as written either way.
        if (next.IsKind(SyntaxKind.IdentifierToken)
            && next.Parent is ParameterSyntax
            || next.IsKind(SyntaxKind.InKeyword)
            && next.Parent is CommonForEachStatementSyntax
            || next.IsKind(SyntaxKind.WhenKeyword)
            && next.Parent is WhenClauseSyntax
            || next.IsKind(SyntaxKind.WithKeyword)
            && next.Parent is WithExpressionSyntax) {
            return true;
        }

        return prev.Kind() switch {
            SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.CommaToken => true,
            SyntaxKind.OpenBraceToken => prev.Parent is InitializerExpressionSyntax,
            SyntaxKind.LessThanToken => IsTypeAngle(prev) || IsBinaryOperator(prev),
            SyntaxKind.EqualsGreaterThanToken => true,
            SyntaxKind.QuestionToken => prev.Parent is ConditionalExpressionSyntax,
            SyntaxKind.ColonToken => prev.Parent
                is ConditionalExpressionSyntax
                    or BaseListSyntax
                    or TypeParameterConstraintClauseSyntax,
            SyntaxKind.CloseParenToken => prev.Parent is CastExpressionSyntax,
            SyntaxKind.SemicolonToken => prev.Parent is ForStatementSyntax,
            SyntaxKind.NewKeyword => prev.Parent is BaseObjectCreationExpressionSyntax,
            SyntaxKind.ReturnKeyword
                or SyntaxKind.ThrowKeyword
                or SyntaxKind.AwaitKeyword
                or SyntaxKind.CaseKeyword
                or SyntaxKind.WhereKeyword
                or SyntaxKind.NotKeyword => true,
            SyntaxKind.InKeyword => prev.Parent is CommonForEachStatementSyntax,
            // ⚠ `is` and `as` are binary operators to Roslyn and not to this gap: `f is/* f */int`
            // comes back `f is /* f */ int`, closed or spaced.
            SyntaxKind.IsKeyword or SyntaxKind.AsKeyword => false,
            _ => IsBinaryOperator(prev)
                || IsAssignmentOperator(prev)
                || IsPrefixOperator(prev)
                || IsMemberAccessPunctuation(prev)
        };
    }

    /// <summary>
    ///     The gaps no rule in the export governs, where the oracle leaves whatever the author wrote.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="SpaceKind.Preserve" /> exists in the IR from milestone 1 and nothing produced it
    ///     until now, which made this class total in the wrong way: every gap got an answer and two of
    ///     them were answers the oracle does not give. Asked directly, <c>[1, ..a]</c> comes back
    ///     <c>..a</c> and <c>[1, ..   a]</c> comes back <c>.. a</c>; <c>a[1..3]</c> stays closed up and
    ///     <c>a[1  ..  3]</c> comes back <c>a[1 .. 3]</c>. That is not a rule with a value, it is
    ///     <c>extra_spaces = remove_all</c> collapsing a run in a gap nobody legislated.
    ///     <para>
    ///         ⚠ A slice pattern is <em>not</em> in this set and looks as though it should be:
    ///         <c>a is [1, ..var r]</c> comes back <c>.. var r</c>, a space the oracle inserts, because
    ///         <c>space_within_slice_pattern = true</c> really does govern that one. Reading
    ///         <c>space_within_spread_pattern</c> as the collection-expression twin of it — which is what
    ///         its name says — is what put a space Skala had no evidence for into 58 lines of
    ///         <c>corpus/real/</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ The gap between a recursive pattern's type and its positional clause is in this set,
    ///         and <see cref="BeforeOpenParen" /> used to answer it with a space — <c>o is Point (2, 3)</c>
    ///         — on the remark that an identifier before a parenthesis that is not a call keeps its gap.
    ///         Asked directly (#373): <c>Point(2, 3)</c> comes back closed, <c>Point (2, 3)</c> comes back
    ///         spaced, and <c>Point  (2, 3)</c> collapses to one space, in every position a pattern can
    ///         take — after <c>is</c>, <c>not</c>, <c>case</c>, inside a tuple pattern, as a switch arm,
    ///         with a property clause behind it and with a generic or qualified type in front. Seven keys
    ///         were flipped against it (<c>space_before_method_call_parentheses</c> and its empty twin,
    ///         <c>space_before_method_parentheses</c> and its empty twin, <c>space_before_new_parentheses</c>,
    ///         <c>space_before_type_parameter_angle</c>, <c>space_before_open_square_brackets</c>); each
    ///         moved its own control and none moved the pattern. That is the same shape as the range
    ///         operator's gap: nothing legislates it, so the author's choice survives. ⚠ Only the typed
    ///         clause. <c>is (1, 2)</c>, <c>case (1, 2)</c> and <c>var (a, b)</c> are governed — the oracle
    ///         puts the space into <c>is(1, 2)</c> and <c>var(a, b)</c> — and stay with the rule.
    ///     </para>
    /// </remarks>
    static bool Ungoverned(SyntaxToken prev, SyntaxToken next) {
        if (prev.IsKind(SyntaxKind.DotDotToken)) {
            return prev.Parent is SpreadElementSyntax or RangeExpressionSyntax { RightOperand: not null };
        }

        if (next.IsKind(SyntaxKind.DotDotToken)) {
            return next.Parent is RangeExpressionSyntax { LeftOperand: not null };
        }

        // A subpattern's colon (#419): `{ X: 1 }`, `{ X : 1 }`, `{ Q.X : 1 }` and `(A : 1, B : _)` all
        // come back as written, a run collapses to one, and no colon key moves it.
        // A label's colon (#433) likewise: `a :`, `a:` and `a /* c */ :` come back as written, `a  :`
        // collapses, and none of the colon, semicolon or label keys moves it.
        if (next.IsKind(SyntaxKind.ColonToken)) {
            return next.Parent is BaseExpressionColonSyntax { Parent: SubpatternSyntax } or LabeledStatementSyntax;
        }

        return next.IsKind(SyntaxKind.OpenParenToken) && FollowsItsPatternType(next);
    }

    /// <summary>
    ///     True for the <c>(</c> of a positional clause whose recursive pattern names a type, which is
    ///     the token in front of it: <c>Point(2, 3)</c>, <c>N.Point(2, 3)</c>, <c>Pair&lt;int, int&gt;(1, 2)</c>.
    /// </summary>
    static bool FollowsItsPatternType(SyntaxToken open) =>
        open.Parent is PositionalPatternClauseSyntax { Parent: RecursivePatternSyntax { Type: not null } };

    static bool Required(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) {
        var left = prev.Kind();
        var right = next.Kind();

        // ── Attributes ───────────────────────────────────────────────────────────────────────
        if (left == SyntaxKind.CloseBracketToken && prev.Parent is AttributeListSyntax) {
            return right == SyntaxKind.OpenBracketToken && next.Parent is AttributeListSyntax
                ? o.SpaceBetweenAttributeSections
                : o.SpaceAfterAttributes;
        }

        if (right == SyntaxKind.ColonToken && next.Parent is AttributeTargetSpecifierSyntax) {
            return o.SpaceBeforeAttributeColon;
        }

        if (left == SyntaxKind.ColonToken && prev.Parent is AttributeTargetSpecifierSyntax) {
            return o.SpaceAfterAttributeColon;
        }

        // ── Expression braces ────────────────────────────────────────────────────────────────
        // ⚠ Ahead of every other rule, because the gap inside one belongs to the brace whatever stands
        // beside it: `{(1, 2)}`, `{[1] = 2}`, `{{1, 2}}` and `{() => { }}` were each answered by the
        // parenthesis, bracket or nested brace and kept their space at the key's `false` (#419).
        if (left == SyntaxKind.OpenBraceToken && IsExpressionBrace(prev.Parent)) {
            return WithinBraces(prev.Parent, next, o);
        }

        if (right == SyntaxKind.CloseBraceToken && IsExpressionBrace(next.Parent)) {
            return WithinBraces(next.Parent, prev, o);
        }

        // ── Parentheses ──────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.OpenParenToken) {
            return BeforeOpenParen(prev, next, o);
        }

        if (left == SyntaxKind.OpenParenToken || right == SyntaxKind.CloseParenToken) {
            var empty = left == SyntaxKind.OpenParenToken && right == SyntaxKind.CloseParenToken;
            return WithinParentheses(left == SyntaxKind.OpenParenToken ? prev.Parent : next.Parent, empty, o);
        }

        // ── Brackets ─────────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.OpenBracketToken) {
            return BeforeOpenBracket(prev, next, o);
        }

        if (left == SyntaxKind.OpenBracketToken) {
            return WithinBrackets(prev.Parent, right == SyntaxKind.CloseBracketToken, o);
        }

        if (right == SyntaxKind.CloseBracketToken) {
            return WithinBrackets(next.Parent, left == SyntaxKind.OpenBracketToken, o);
        }

        // ── Commas and semicolons ────────────────────────────────────────────────────────────
        if (right == SyntaxKind.CommaToken) {
            return o.SpaceBeforeComma;
        }

        if (left == SyntaxKind.CommaToken) {
            // ⚠ `typeof(ValueTuple<,>)` — an unbound generic name's type argument list is nothing
            // but commas and zero-width `OmittedTypeArgumentSyntax` nodes, so "a space follows a
            // comma" writes `ValueTuple<, >`. The equivalent `int[,]` is already correct because a
            // rank specifier's `]` is handled above; the angle brackets fall through to here.
            return prev.Parent is not TypeArgumentListSyntax { Arguments: [OmittedTypeArgumentSyntax, ..] }
                && o.SpaceAfterComma;
        }

        // ⚠ A label's empty statement is not a semicolon's gap: `e:;`, `e: ;` and `e:   ;` all come back
        // `e: ;`, and `space_before_semicolon` moves every other `;` and not this one (#433).
        if (right == SyntaxKind.SemicolonToken && next.Parent is EmptyStatementSyntax { Parent: LabeledStatementSyntax }) {
            return true;
        }

        if (right == SyntaxKind.SemicolonToken) {
            return next.Parent is ForStatementSyntax ? o.SpaceBeforeSemicolonInFor : o.SpaceBeforeSemicolon;
        }

        if (left == SyntaxKind.SemicolonToken) {
            if (prev.Parent is ForStatementSyntax) {
                return o.SpaceAfterSemicolonInFor;
            }

            // `{ get; set; }` — the gap *between* two accessors written on one line, and only that
            // one. ⚠ The `right != CloseBraceToken` guard is the whole point: the gap in front of the
            // holder's `}` used to be answered here too, and it belongs to
            // `space_in_singleline_accessorholder`, which owns both of the holder's inner gaps.
            // Measured on `public int X { get; set; }`, one key flipped at a time over the export:
            // `space_between_accessors_in_singleline_property = false` gives `{ get;set; }` — the
            // brace's own space survives — and `space_in_singleline_accessorholder = false` gives
            // `{get; set;}`, closing both ends at once. Skala answered the first `{ get;set;}` and
            // the second `{get; set; }`, so neither key could produce either of the oracle's shapes.
            if (right != SyntaxKind.CloseBraceToken
                && prev.Parent is AccessorDeclarationSyntax { Parent: AccessorListSyntax }) {
                return o.SpaceBetweenAccessorsInSinglelineProperty;
            }

            // ⚠ `get { return _n; }` — a semicolon in front of a closing brace is the brace's gap,
            // not a semicolon's. Answering `true` here made the inside of a single-line accessor
            // body asymmetric: the `{` side was governed and the `}` side was not, so no
            // configuration could produce the `get {return _n;}` the oracle writes.
            return right != SyntaxKind.CloseBraceToken || WithinBraces(next.Parent, prev, o);
        }

        // `using Alias = System.Text;` — the alias's own equals sign, not an assignment.
        if (prev.Parent is NameEqualsSyntax { Parent: UsingDirectiveSyntax }
            || next.Parent is NameEqualsSyntax { Parent: UsingDirectiveSyntax }) {
            return o.SpaceAroundAliasEq;
        }

        // ── Spread and range ─────────────────────────────────────────────────────────────────
        // `a is [1, .. var rest]` — space_within_slice_pattern = true. A collection expression's
        // spread never reaches here: `Ungoverned` answered it before `Required` was called.
        if (left == SyntaxKind.DotDotToken) {
            return SpreadSpacing(prev, o);
        }

        if (right == SyntaxKind.DotDotToken) {
            return SpreadSpacing(next, o);
        }

        // ── Member access ────────────────────────────────────────────────────────────────────
        // ⚠ Two keys, not one. `space_around_member_access_operator` is the generalized name for
        // both and the resolver still expands it, but `space_around_dot` and `space_around_arrow_op`
        // are separately answerable by the oracle and were both being ignored.
        if (IsMemberAccessPunctuation(prev) || IsMemberAccessPunctuation(next)) {
            var arrow = prev.IsKind(SyntaxKind.MinusGreaterThanToken)
                || next.IsKind(SyntaxKind.MinusGreaterThanToken);
            return arrow ? o.SpaceAroundArrowOp : o.SpaceAroundDot;
        }

        // ── Question marks ───────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.QuestionToken) {
            return next.Parent switch {
                ConditionalExpressionSyntax => o.SpaceBeforeTernaryQuest,
                NullableTypeSyntax => o.SpaceBeforeNullableMark,
                _ => false
            };
        }

        if (left == SyntaxKind.QuestionToken) {
            return prev.Parent switch {
                ConditionalExpressionSyntax => o.SpaceAfterTernaryQuest,
                NullableTypeSyntax => !ClingsLeft(right) && !IsTypeAngle(next),
                _ => true
            };
        }

        // ── Angles ───────────────────────────────────────────────────────────────────────────
        if (right is SyntaxKind.LessThanToken && IsTypeAngle(next)) {
            return next.Parent is TypeParameterListSyntax
                ? o.SpaceBeforeTypeParameterAngle
                : o.SpaceBeforeTypeArgumentAngle;
        }

        if (IsTypeAngle(next) || IsTypeAngle(prev)) {
            if (IsTypeAngle(next)) {
                return WithinAngles(next.Parent, o);
            }

            if (left == SyntaxKind.LessThanToken) {
                return WithinAngles(prev.Parent, o);
            }

            // After the closing `>`: whatever follows decides.
            return !ClingsLeft(right);
        }

        // ⚠ Colons before braces, because a case label's pattern can end with one:
        // `case NamedTypeSymbol { TypeKind: TypeKind.Enum }:` reached the brace rule first, which
        // asks only what clings to the left, and put a space in front of the colon.
        // ── Colons ───────────────────────────────────────────────────────────────────────────
        // ⚠ A constructor initializer's colon is the inheritance clause's, on both sides, and a named
        // argument's — in a call, an attribute or a tuple — is the attribute colon's (#419). Measured
        // with every colon key flipped one at a time over the export, each colon written closed and
        // spaced on both sides: `space_before_colon_in_inheritance_clause = false` gives `C(): base()`
        // and `C(string s): this()` beside `class C: B`, its `after` twin gives `C() :base()`, and
        // `space_before_attribute_colon = true` gives `M(a : 1)`, `(a : 1, b : 2)` and
        // `[Obsolete("x", error : false)]` beside `[return : Obsolete]`. The remark that used to stand
        // here — "C# spends one space here whatever it says" — measured
        // `space_before_colon_in_ctor_initializer`, which really is inert, and never asked this key. A
        // primary constructor's or a record's base call needs no arm: its colon is the base list's.
        // The colon in a property or positional subpattern is no key's: `Ungoverned` keeps the author's
        // gap in front of it, and one space follows it at every value of every colon key.
        if (right == SyntaxKind.ColonToken) {
            return next.Parent switch {
                BaseListSyntax or ConstructorInitializerSyntax => o.SpaceBeforeColonInInheritance,
                TypeParameterConstraintClauseSyntax => o.SpaceBeforeTypeParameterConstraintColon,
                NameColonSyntax { Parent: ArgumentSyntax or AttributeArgumentSyntax } => o.SpaceBeforeAttributeColon,
                SwitchLabelSyntax => o.SpaceBeforeColonInCase,
                ConditionalExpressionSyntax => o.SpaceBeforeTernaryColon,
                _ => false
            };
        }

        if (left == SyntaxKind.ColonToken) {
            return prev.Parent switch {
                BaseListSyntax or ConstructorInitializerSyntax => o.SpaceAfterColonInInheritance,
                TypeParameterConstraintClauseSyntax => o.SpaceAfterTypeParameterConstraintColon,
                NameColonSyntax { Parent: ArgumentSyntax or AttributeArgumentSyntax } => o.SpaceAfterAttributeColon,
                SwitchLabelSyntax => o.SpaceAfterColonInCase,
                ConditionalExpressionSyntax => o.SpaceAfterTernaryColon,
                _ => true
            };
        }

        // ── Braces ───────────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.OpenBraceToken) {
            return BeforeOpenBrace(prev, next, o);
        }

        if (left == SyntaxKind.OpenBraceToken) {
            return WithinBraces(prev.Parent, next, o);
        }

        if (right == SyntaxKind.CloseBraceToken) {
            return WithinBraces(next.Parent, prev, o);
        }

        if (left == SyntaxKind.CloseBraceToken) {
            return !ClingsLeft(right);
        }

        // ── Operators ────────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.EqualsGreaterThanToken || left == SyntaxKind.EqualsGreaterThanToken) {
            return o.SpaceAroundLambdaArrow;
        }

        if (IsPostfixOperator(next)) {
            return o.SpaceNearPostfixAndPrefixOp;
        }

        if (IsPostfixOperator(prev)) {
            return !ClingsLeft(right);
        }

        if (IsPrefixOperator(prev) || left == SyntaxKind.TildeToken && prev.Parent is DestructorDeclarationSyntax) {
            return AfterPrefixOperator(prev, o);
        }

        if (IsPrefixOperator(next)) {
            return !ClingsRight(left);
        }

        // ⚠ `space_before_pointer_asterik_declaration` governs the gap in front of the `*` and there
        // is no key for the one behind it, because behind it is an ordinary "a type is followed by a
        // name" gap. Answering both sides from the one key writes `int*p` and `void M(int**q)`.
        if (IsPointerDeclarator(next)) {
            return o.SpaceBeforePointerAsterikDeclaration;
        }

        if (IsPointerDeclarator(prev)) {
            return !ClingsLeft(right);
        }

        if (IsBinaryOperator(prev) || IsBinaryOperator(next)) {
            // ⚠ The gap *behind* a right-shift is not the shift operator's, and this asymmetry is
            // measured rather than inferred. `>>` and `>>>` are two and three `>` tokens to the
            // parser that has to tell `List<List<int>>` from a shift, and ReSharper resolves the gap
            // after the last of them as the gap after a closing angle bracket — whatever follows
            // decides — instead of as the right-hand side of a shift. Measured at
            // `space_around_shift_op = false` on `a << 2 >> 1`, `a >> b`, `a >>> 1`, `a >> (b + 1)`,
            // `a >> -b` and `a >> 1L`: the oracle writes `a<<2>> 1`, `a>> b`, `a>>> 1`, `a>> (b + 1)`,
            // `a>> -b` and `a>> 1L`. Every left-hand gap closes; every gap behind a `>>` survives,
            // including the one in front of a `(`, which `space_around_shift_op = true` would have
            // written identically. `<<` has no such split and closes on both sides.
            if (IsBinaryOperator(prev)
                && prev.Kind() is SyntaxKind.GreaterThanGreaterThanToken
                or SyntaxKind.GreaterThanGreaterThanGreaterThanToken) {
                return !ClingsLeft(right);
            }

            return BinarySpacing(IsBinaryOperator(prev) ? prev : next, o);
        }

        if (IsAssignmentOperator(prev) || IsAssignmentOperator(next)) {
            return o.SpaceAroundAssignmentOp;
        }

        if (left == SyntaxKind.OperatorKeyword) {
            return o.SpaceAfterOperatorKeyword;
        }

        // ── Casts ────────────────────────────────────────────────────────────────────────────
        if (left == SyntaxKind.CloseParenToken && prev.Parent is CastExpressionSyntax) {
            return o.SpaceAfterCast;
        }

        if (left is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken) {
            return !ClingsLeft(right);
        }

        // ── Fallback ─────────────────────────────────────────────────────────────────────────
        if (ClingsLeft(right) || ClingsRight(left)) {
            // ⚠ Before the keyword rule, not after: `global::System` starts with a keyword and the
            // `::` still clings.
            return false;
        }

        // A keyword and its operand. The two options are told apart by the keyword, not by the
        // operand's node type: `return a;` has an IdentifierNameSyntax after it, and an
        // IdentifierNameSyntax is a TypeSyntax.
        if (SyntaxFacts.IsKeywordKind(left)) {
            return IntroducesAType(left) ? o.SpaceBetweenKeywordAndType : o.SpaceBetweenKeywordAndExpression;
        }


        return true;
    }

    static bool IntroducesAType(SyntaxKind keyword) =>
        keyword is
        SyntaxKind.NewKeyword
            or SyntaxKind.IsKeyword
            or SyntaxKind.AsKeyword
            or SyntaxKind.StackAllocKeyword
            or SyntaxKind.TypeOfKeyword
            or SyntaxKind.SizeOfKeyword
            or SyntaxKind.DefaultKeyword
            or SyntaxKind.RefKeyword
            or SyntaxKind.OutKeyword
            or SyntaxKind.InKeyword
            or SyntaxKind.ScopedKeyword
            or SyntaxKind.ParamsKeyword
            or SyntaxKind.ReadOnlyKeyword
            or SyntaxKind.ConstKeyword
            or SyntaxKind.WhereKeyword;

    /// <summary>
    ///     The gap around a <c>..</c>. ⚠ A prefix range with no left operand — which is how Roslyn
    ///     parses a spread inside an array initializer — is a spread, not a range, and gets the space.
    /// </summary>
    /// <summary>
    ///     The gap beside a <c>..</c> that a rule really does govern: a slice pattern's.
    /// </summary>
    /// <remarks>
    ///     ⚠ A collection expression's spread element used to be answered here too, out of
    ///     <c>space_within_spread_pattern</c>. It is not governed at all — see <see cref="Ungoverned" />
    ///     — and the key is inert at both values.
    /// </remarks>
    static bool SpreadSpacing(SyntaxToken token, in PhaseOneOptions o) =>
        token.Parent is SlicePatternSyntax && o.SpaceWithinSlicePattern;

    /// <summary>Tokens that never take a space on their left.</summary>
    static bool ClingsLeft(SyntaxKind kind) =>
        kind is SyntaxKind.SemicolonToken
            or SyntaxKind.CommaToken
            or SyntaxKind.CloseParenToken
            or SyntaxKind.CloseBracketToken
            or SyntaxKind.DotToken
            or SyntaxKind.ColonColonToken
            or SyntaxKind.DotDotToken
            or SyntaxKind.MinusGreaterThanToken
            or SyntaxKind.ExclamationToken
            or SyntaxKind.PlusPlusToken
            or SyntaxKind.MinusMinusToken;

    /// <summary>Tokens that never take a space on their right.</summary>
    static bool ClingsRight(SyntaxKind kind) =>
        kind is SyntaxKind.OpenParenToken
            or SyntaxKind.OpenBracketToken
            or SyntaxKind.DotToken
            or SyntaxKind.ColonColonToken
            or SyntaxKind.DotDotToken
            or SyntaxKind.MinusGreaterThanToken;

    static bool BeforeOpenParen(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) {
        // `!(value is T x)` — a prefix operator binds to its operand whatever the operand is, and
        // the operand being parenthesised does not change that.
        if (IsPrefixOperator(prev)) {
            return AfterPrefixOperator(prev, o);
        }

        switch (prev.Kind()) {
            // ⚠ Nine keys rather than the one generalized `space_after_keywords_in_control_flow_
            // statements` these used to share. The oracle answers each keyword on its own —
            // `space_before_if_parentheses = false` produces `if(n > 0)` and leaves `while (…)`
            // alone — so the shared answer was eight keys ignored.
            case SyntaxKind.IfKeyword:
                return o.SpaceBeforeIfParentheses;

            case SyntaxKind.WhileKeyword:
                return o.SpaceBeforeWhileParentheses;

            case SyntaxKind.ForKeyword:
                return o.SpaceBeforeForParentheses;

            case SyntaxKind.ForEachKeyword:
                return o.SpaceBeforeForeachParentheses;

            case SyntaxKind.SwitchKeyword:
                return o.SpaceBeforeSwitchParentheses;

            case SyntaxKind.CatchKeyword:
                return o.SpaceBeforeCatchParentheses;

            case SyntaxKind.LockKeyword:
                return o.SpaceBeforeLockParentheses;

            case SyntaxKind.UsingKeyword:
                return o.SpaceBeforeUsingParentheses;

            case SyntaxKind.FixedKeyword:
                return o.SpaceBeforeFixedParentheses;

            case SyntaxKind.TypeOfKeyword:
                return o.SpaceBeforeTypeofParentheses;

            case SyntaxKind.SizeOfKeyword:
                return o.SpaceBeforeSizeofParentheses;

            case SyntaxKind.DefaultKeyword:
                return o.SpaceBeforeDefaultParentheses;

            case SyntaxKind.CheckedKeyword:
            case SyntaxKind.UncheckedKeyword:
                return o.SpaceBeforeCheckedParentheses;

            case SyntaxKind.NewKeyword:
                // ⚠ `new (string Name, int Value)[] { … }` — the parenthesis opens a tuple *type*,
                // not an argument list, so `space_before_new_parentheses = false` has nothing to say
                // about it and closing it up produces `new(string Name, int Value)[]`, which reads
                // as an implicit object creation and is not one.
                return next.Parent is TupleTypeSyntax || o.SpaceBeforeNewParentheses;
        }

        if (prev.Text is "nameof") {
            return o.SpaceBeforeNameofParentheses;
        }

        var empty = next.Parent is BaseArgumentListSyntax { Arguments.Count: 0 }
            or BaseParameterListSyntax { Parameters.Count: 0 };

        switch (next.Parent) {
            case ParameterListSyntax { Parent: ParenthesizedLambdaExpressionSyntax }:
                // ⚠ A lambda's parentheses are the head of an operand, not a call site: whatever
                // precedes decides. `x += (a, b) => …` needs its space; `M((a, b) => …)` does not.
                return !ClingsRight(prev.Kind()) && !IsCallSite(prev);

            case ParameterListSyntax or FunctionPointerParameterListSyntax:
                return empty ? o.SpaceBeforeEmptyMethodParentheses : o.SpaceBeforeMethodParentheses;

            // ⚠ An *implicit* object creation only. `new C()`'s parentheses are an ordinary call
            // site's and fall through to the case below. Measured on `new C()`, `new C(1)`,
            // `new()`, `new(1)`, `new List<int>()` and `new int[4]` in one file:
            // `space_before_new_parentheses = true` gives `new ()` and `new (1)` and leaves
            // `new C()` and `new List<int>()` shut, while
            // `space_before_method_call_parentheses = true` with its empty twin opens exactly those
            // two — `new C ()`, `new C (1)`, `new List<int> ()` — and leaves `new()` alone. Reading
            // an explicit creation out of this key gave `new C ()` at every `true`, which no
            // configuration of the oracle's produces. The `ImplicitObjectCreationExpressionSyntax`
            // arm is kept because a tree can reach it, though `new`'s own keyword case above
            // normally answers first.
            case ArgumentListSyntax { Parent: ImplicitObjectCreationExpressionSyntax }:
                return o.SpaceBeforeNewParentheses;

            case ArgumentListSyntax or AttributeArgumentListSyntax:
                return empty ? o.SpaceBeforeEmptyMethodCallParentheses : o.SpaceBeforeMethodCallParentheses;

            case ParenthesizedVariableDesignationSyntax:
            case PositionalPatternClauseSyntax:
                // ⚠ `var (a, b) = …`, `is (1, 2)` and `case (1, 2)`: what precedes is a keyword or a
                // separator, not a callee, and the oracle puts the space into `var(a, b)` and
                // `is(1, 2)` alike. ⚠ This arm used to name `is Point (1, 2)` as its own example and
                // answer it the same way; a clause that follows its pattern's *type* never reaches
                // here — the gap is ungoverned and `Ungoverned` resolves it against the source (#373).
                return !ClingsRight(prev.Kind());

            default:
                if (IsTypeAngle(prev)) {
                    // `List<(int, int)>` — a tuple type as a type argument.
                    return WithinAngles(prev.Parent, o);
                }

                // An operand in parentheses: whatever precedes it decides — including a keyword,
                // which is the one case the general fallback below never sees because a `(` is
                // handled here.
                if (SyntaxFacts.IsKeywordKind(prev.Kind()) && !IntroducesAType(prev.Kind())) {
                    return o.SpaceBetweenKeywordAndExpression;
                }

                return !ClingsRight(prev.Kind()) && !IsCallSite(prev);
        }
    }

    /// <summary>
    ///     True when <paramref name="prev" /> would make the following <c>(</c> read as a call.
    /// </summary>
    /// <remarks>
    ///     ⚠ A <c>&gt;</c> qualifies only when it closes a type argument list — <c>Foo&lt;int&gt;(x)</c>
    ///     is a call and <c>count &gt; (buffer.Length - index)</c> is a comparison. Treating every
    ///     <c>&gt;</c> as a call site removed the space after the operator and produced
    ///     <c>count &gt;(buffer.Length - index)</c>. It survived milestone 3 because every corpus line
    ///     that shows it sits inside a <c>#if</c> body, which the formatter could not see until M5
    ///     supplied preprocessor symbols — the symbols did not cause the bug, they revealed it.
    /// </remarks>
    static bool IsCallSite(SyntaxToken prev) =>
        prev.Kind() is SyntaxKind.IdentifierToken or SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken
        || IsTypeAngle(prev);

    static bool BeforeOpenBracket(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) =>
        prev.IsKind(SyntaxKind.OpenBraceToken)
            // `new Dictionary<…> { ["a"] = 1 }` — the gap belongs to the brace, not to the bracket.
            ? WithinBraces(prev.Parent, next, o)
            : next.Parent switch {
                // ⚠ `void D<[Obsolete] T>()` — an attribute list that opens a type parameter list sits
                // just inside the angle, and that gap is the angle's: measured with
                // `space_within_type_parameter_angles = true` the oracle writes `D< [Obsolete] T >`, and
                // at `false` it closes `D< [Obsolete] T>` up. Answering it as "whatever precedes" put
                // a space after every leading `<` because `<` clings to nothing on its own (#373).
                // `E<T, [Obsolete] U>` was never affected: its attribute follows a comma.
                AttributeListSyntax when IsTypeAngle(prev) => WithinAngles(prev.Parent, o),
                AttributeListSyntax => !ClingsRight(prev.Kind()),
                // ⚠ Only a rank specifier that carries no sizes. Measured on `int[] a`, `int[,] b`,
                // `int[][] c`, `new int[] { 1 }`, `new int[4]` and `new int[2, 2]` in one file: at
                // `space_before_array_rank_brackets = true` the oracle writes `int [] a`,
                // `int [,] b`, `int [] [] c` and `new int [] { 1 }` — and leaves `new int[4]` and
                // `new int[2, 2]` shut. The key is about the brackets that spell an array *type*;
                // the ones that carry a creation's lengths are not rank brackets to ReSharper, and
                // reading them out of this key cost `new int [4]` at every `true`.
                ArrayRankSpecifierSyntax rank => IsOmittedRank(rank) && o.SpaceBeforeArrayRankBrackets,
                // ⚠ `{ [key] = v, [key2] = v2 }` — an implicit element access has no operand in
                // front of it, so `space_before_array_access_brackets` has no gap to govern and
                // whatever precedes decides. Sharing the rule with a real `a[i]` deletes the space
                // after the separating comma, which is 55 lines of `corpus/real/`. ⚠ The `[` of an
                // implicit element access hangs from its `BracketedArgumentListSyntax` like any
                // other indexer's, so the two are told apart by that list's own parent.
                BracketedArgumentListSyntax { Parent: ImplicitElementAccessSyntax } => !ClingsRight(prev.Kind()),
                BracketedArgumentListSyntax => o.SpaceBeforeArrayAccessBrackets,
                ImplicitElementAccessSyntax => !ClingsRight(prev.Kind()),
                BracketedParameterListSyntax => o.SpaceBeforeMethodParentheses,
                // ⚠ A cast's closing parenthesis is not a call site, and this is the one place the
                // difference shows: `(IrBindingKind[]) [a, b]` comes back from the oracle with the
                // space, because the bracket is the cast's operand rather than an indexer on its
                // result. `a[i]` and `M()[i]` still close up.
                CollectionExpressionSyntax or ListPatternSyntax => !ClingsRight(prev.Kind()) && !IsCallSite(prev),
                // ⚠ `space_before_open_square_brackets` is the generalized name for the two keys
                // above it and is honoured by the resolver expanding it into them, so the fallback
                // is the access-bracket key rather than a third reading of the same setting.
                _ => o.SpaceBeforeArrayAccessBrackets
            };

    /// <summary>
    ///     The gap just inside a parenthesis, decided by what the parenthesis belongs to.
    /// </summary>
    /// <remarks>
    ///     ⚠ Fifteen keys where <c>space_within_parentheses</c> used to answer for all of them, which
    ///     left every one of the fifteen inert. Each was asked of the oracle on its own before being
    ///     wired: <c>space_within_if_parentheses = true</c> gives <c>if ( n &gt; 0 )</c> and nothing
    ///     else moves. <c>space_within_parentheses</c> keeps the gap it really owns — a parenthesized
    ///     expression's, which is what its own fixture pins.
    ///     <para>
    ///         ⚠ <paramref name="empty" /> is a separate question rather than "no space": the oracle writes
    ///         <c>Empty( )</c> and <c>new object( )</c> when the empty-parentheses keys are set, so an empty
    ///         pair is governed rather than always closed up.
    ///     </para>
    /// </remarks>
    static bool WithinParentheses(SyntaxNode? owner, bool empty, in PhaseOneOptions o) =>
        owner switch {
            // A call's parentheses, including an object creation's and an attribute's.
            ArgumentListSyntax {
                Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } }
            } =>
                !empty && o.SpaceWithinNameofParentheses,
            ArgumentListSyntax or AttributeArgumentListSyntax =>
                empty ? o.SpaceWithinEmptyMethodCallParentheses : o.SpaceWithinMethodCallParentheses,
            ParameterListSyntax or BracketedParameterListSyntax or FunctionPointerParameterListSyntax =>
                empty ? o.SpaceWithinEmptyMethodDeclarationParentheses : o.SpaceWithinMethodDeclarationParentheses,

            // An empty pair below this line cannot occur — a statement's condition, a cast's type
            // and a `typeof`'s operand are all non-empty by the grammar — so they answer `!empty &&`
            // only so that a malformed tree cannot open a gap the option never asked for.
            CastExpressionSyntax => !empty && o.SpaceBetweenTypecastParentheses,
            IfStatementSyntax => !empty && o.SpaceWithinIfParentheses,
            WhileStatementSyntax or DoStatementSyntax => !empty && o.SpaceWithinWhileParentheses,
            ForStatementSyntax => !empty && o.SpaceWithinForParentheses,
            CommonForEachStatementSyntax => !empty && o.SpaceWithinForeachParentheses,
            SwitchStatementSyntax => !empty && o.SpaceWithinSwitchParentheses,
            CatchDeclarationSyntax => !empty && o.SpaceWithinCatchParentheses,
            LockStatementSyntax => !empty && o.SpaceWithinLockParentheses,
            UsingStatementSyntax => !empty && o.SpaceWithinUsingParentheses,
            FixedStatementSyntax => !empty && o.SpaceWithinFixedParentheses,
            CheckedExpressionSyntax => !empty && o.SpaceWithinCheckedParentheses,
            DefaultExpressionSyntax => !empty && o.SpaceWithinDefaultParentheses,
            SizeOfExpressionSyntax => !empty && o.SpaceWithinSizeofParentheses,
            TypeOfExpressionSyntax => !empty && o.SpaceWithinTypeofParentheses,
            _ => !empty && o.SpaceWithinParentheses
        };

    /// <summary>The gap just inside a bracket, on whichever side.</summary>
    static bool WithinBrackets(SyntaxNode? owner, bool empty, in PhaseOneOptions o) =>
        owner switch {
            AttributeListSyntax => !empty && o.SpaceWithinAttributeBrackets,
            ListPatternSyntax => !empty && o.SpaceWithinListPatternBrackets,
            BracketedArgumentListSyntax or ImplicitElementAccessSyntax =>
                !empty && o.SpaceWithinArrayAccessBrackets,
            // ⚠ `new[]` is the empty key's and `int[,]` is not, which is what the oracle answers:
            // the line is "one omitted size", not "no sizes". Reading it as "every size omitted"
            // puts `int[ , ]` under the empty key, where flipping it does nothing.
            ArrayRankSpecifierSyntax rank =>
                IsEmptyRank(rank) ? o.SpaceWithinArrayRankEmptyBrackets : !empty && o.SpaceWithinArrayRankBrackets,
            CollectionExpressionSyntax => o.SpaceWithinSlicePattern && false,
            _ => false
        };

    static bool IsEmptyRank(ArrayRankSpecifierSyntax rank) => rank.Sizes is [OmittedArraySizeExpressionSyntax];

    /// <summary>
    ///     A rank specifier that spells a type rather than a length — <c>[]</c>, <c>[,]</c>, <c>[,,]</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="IsEmptyRank" />, which is one omitted size and answers a different key.
    ///     <c>int[,]</c> carries two omitted sizes and is still a type, so this is "every size
    ///     omitted" where that one is "exactly one".
    /// </remarks>
    static bool IsOmittedRank(ArrayRankSpecifierSyntax rank) =>
        rank.Sizes.All(static size => size is OmittedArraySizeExpressionSyntax);

    static bool WithinAngles(SyntaxNode? owner, in PhaseOneOptions o) =>
        owner is TypeParameterListSyntax ? o.SpaceWithinTypeParameterAngles : o.SpaceWithinTypeArgumentAngles;

    /// <remarks>
    ///     ⚠ <c>space_before_singleline_accessorholder</c> used to be read here and is not, because the
    ///     C# formatter does not answer to it. Measured on <c>public int X { get; set; }</c>,
    ///     <c>public int Y{ get; set; }</c>, a single-line indexer and a single-line event, one key
    ///     flipped at a time over the export: at <c>true</c> and at <c>false</c> alike the oracle
    ///     returns one space in front of every accessor holder's brace — it puts the space into
    ///     <c>Y{</c> and it never takes the one in <c>X {</c> away. The gap in front of a brace that
    ///     opens on its own line is brace placement's, and ReSharper spends exactly one space on it.
    ///     ⚠ It is registered <c>OfInert</c> rather than deleted: the key is in ReSharper's own export
    ///     and in JetBrains' C# spaces schema, so refusing to resolve it would reject a configuration
    ///     the standard writes.
    /// </remarks>
    static bool BeforeOpenBrace(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) =>
        next.Parent switch {
            AccessorListSyntax => true,
            _ => !ClingsRight(prev.Kind())
        };

    /// <summary>The gap just inside a brace, on whichever side.</summary>
    static bool WithinBraces(SyntaxNode? owner, SyntaxToken other, in PhaseOneOptions o) {
        // `{ }` — empty_block_style = together with space_within_empty_braces = true. ⚠ Only the brace's
        // own partner makes the pair empty: in `{ 1 }}` and `{{1, 2}` the two braces belong to two
        // pairs, and reading any brace beside a brace as empty wrote `new P { Q = { X = 1 }}` at
        // `space_within_empty_braces = false`, where the oracle keeps both spaces (#419).
        if (other.Kind() is SyntaxKind.CloseBraceToken or SyntaxKind.OpenBraceToken && other.Parent == owner) {
            return o.SpaceWithinEmptyBraces;
        }

        return owner switch {
            // ⚠ An accessor's *body* braces, not just the holder's. Measured: with
            // `space_in_singleline_accessorholder = false` the oracle writes `get {return _n;}`,
            // and with `space_in_singleline_method = false` it writes `get { return _n; }` —
            // unchanged. Reading the body out of the method key gave Skala a setting Rider ignores
            // and left the accessor key answering only `{ get; set; }`.
            AccessorListSyntax or BlockSyntax { Parent: AccessorDeclarationSyntax } =>
                o.SpaceInSinglelineAccessorholder,
            BlockSyntax {
                Parent:
                AnonymousMethodExpressionSyntax
                    or SimpleLambdaExpressionSyntax
                    or ParenthesizedLambdaExpressionSyntax
            } =>
                o.SpaceInSinglelineAnonymousMethod,
            BlockSyntax { Parent: BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax } =>
                o.SpaceInSinglelineMethod,
            // ⚠ Every expression brace, not only an array's or a collection's (#419). Measured with
            // `space_within_single_line_array_initializer_braces = false`: an object initializer, a
            // nested member initializer, a dictionary's `[1] = 2` and `{ 1, 2 }` elements, an anonymous
            // object, a `with` and a property pattern all close up — `new P {X = 1}`, `new {X = 1}`,
            // `r with {A = 2}`, `o is P {X: 1}` — and no other brace key moves them.
            _ when IsExpressionBrace(owner) => o.SpaceWithinSingleLineArrayInitializerBraces,
            _ => true
        };
    }

    /// <summary>
    ///     A brace pair that delimits an expression's or a pattern's contents rather than a body, whose
    ///     inside the array-initializer key governs whatever kind of initializer it is.
    /// </summary>
    static bool IsExpressionBrace(SyntaxNode? owner) =>
        owner is InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax or PropertyPatternClauseSyntax;

    static bool IsMemberAccessPunctuation(SyntaxToken token) =>
        token.Kind() is SyntaxKind.DotToken or SyntaxKind.MinusGreaterThanToken
        || token.IsKind(SyntaxKind.QuestionToken)
        && token.Parent is ConditionalAccessExpressionSyntax;

    static bool IsTypeAngle(SyntaxToken token) =>
        token.Kind() is SyntaxKind.LessThanToken or SyntaxKind.GreaterThanToken
        && token.Parent is TypeArgumentListSyntax
            or TypeParameterListSyntax
            or FunctionPointerParameterListSyntax
            or FunctionPointerUnmanagedCallingConventionListSyntax;

    /// <summary>
    ///     The gap behind a prefix operator, which ReSharper spells one key per operator.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>space_after_unary_operator</c> is the <em>generalized</em> key, and the export writes
    ///     all six lines. Reading only the generalized one answered every operator with one value,
    ///     which is right at this export's defaults — all six are <c>false</c> — and wrong the moment
    ///     one of them is not. Measured: <c>space_after_logical_not_op = true</c> alone produces
    ///     <c>! b</c> and leaves <c>-a</c>, <c>+a</c>, <c>&amp;a</c> and <c>*p</c> untouched, and the
    ///     other four are the same story one operator over.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             <c>~</c> and the prefix <c>++</c>/<c>--</c> are not what this key governs, and the
    ///             note that used to stand here said the opposite.
    ///         </b> It read "they keep reading the
    ///         generalized key … that is a divergence Skala has always had", and the generalized key
    ///         is exactly what they must not read. Measured against `jb cleanupcode` 2025.2.6 under
    ///         the doc-free format-only profile, one key flipped at a time over the export, on
    ///         <c>!a</c>, <c>-b</c>, <c>+b</c>, <c>~b</c>, <c>++b</c>, <c>--b</c>, <c>*p</c> and
    ///         <c>&amp;b</c> in one file: at <c>space_after_unary_operator = true</c> the oracle
    ///         writes <c>! a</c>, <c>- b</c>, <c>+ b</c>, <c>* p</c> and <c>&amp; b</c> — and returns
    ///         <c>~b</c>, <c>++b</c> and <c>--b</c> untouched. The prefix <c>++</c>/<c>--</c> have
    ///         their own key and it moves them: <c>space_near_postfix_and_prefix_op = true</c> on the
    ///         same file gives <c>++ b</c> and <c>-- b</c> while every other operator stays shut.
    ///         <c>~</c> has no key at all; the oracle never spaces it.
    ///     </para>
    /// </remarks>
    static bool AfterPrefixOperator(SyntaxToken op, in PhaseOneOptions o) =>
        op.Kind() switch {
            SyntaxKind.ExclamationToken => o.SpaceAfterLogicalNotOp,
            SyntaxKind.MinusToken => o.SpaceAfterUnaryMinusOp,
            SyntaxKind.PlusToken => o.SpaceAfterUnaryPlusOp,
            SyntaxKind.AmpersandToken => o.SpaceAfterAmpersandOp,
            SyntaxKind.AsteriskToken => o.SpaceAfterAsterikOp,
            SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken => o.SpaceNearPostfixAndPrefixOp,
            // ⚠ `~b`, and a destructor's `~C`. No key of ReSharper's reaches the gap behind a
            // bitwise complement, so there is nothing for a configuration to say about it.
            SyntaxKind.TildeToken => false,
            _ => o.SpaceAfterUnaryOperator
        };

    static bool IsPrefixOperator(SyntaxToken token) =>
        token.Parent is PrefixUnaryExpressionSyntax prefix && prefix.OperatorToken == token;

    static bool IsPostfixOperator(SyntaxToken token) =>
        token.Parent is PostfixUnaryExpressionSyntax postfix && postfix.OperatorToken == token;

    /// <summary>
    ///     The <c>*</c> of a pointer type, <c>delegate*</c>'s included.
    /// </summary>
    /// <remarks>
    ///     ⚠ A function pointer's asterisk hangs from <see cref="FunctionPointerTypeSyntax" /> rather
    ///     than from a <see cref="PointerTypeSyntax" />, so it fell through to the operator rules and
    ///     came back as a multiplication: <c>readonly delegate * unmanaged &lt; nint, nint &gt; f;</c>.
    /// </remarks>
    static bool IsPointerDeclarator(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AsteriskToken)
        && token.Parent is PointerTypeSyntax or FunctionPointerTypeSyntax;

    static bool IsBinaryOperator(SyntaxToken token) =>
        token.Parent is BinaryExpressionSyntax binary
        && binary.OperatorToken == token
        || token.Parent is BinaryPatternSyntax pattern
        && pattern.OperatorToken == token
        || token.Parent is RelationalPatternSyntax relational
        && relational.OperatorToken == token;

    static bool BinarySpacing(SyntaxToken op, in PhaseOneOptions o) =>
        op.Kind() switch {
            SyntaxKind.PlusToken or SyntaxKind.MinusToken => o.SpaceAroundAdditiveOp,
            SyntaxKind.LessThanLessThanToken
                or SyntaxKind.GreaterThanGreaterThanToken
                or SyntaxKind.GreaterThanGreaterThanGreaterThanToken => o.SpaceAroundShiftOp,
            SyntaxKind.LessThanToken
                or SyntaxKind.GreaterThanToken
                or SyntaxKind.LessThanEqualsToken
                or SyntaxKind.GreaterThanEqualsToken => o.SpaceAroundRelationalOp,
            // ⚠ `==` and `!=` are not relational operators as far as ReSharper is concerned, and
            // reading them out of `space_around_relational_op` is a scope error rather than a
            // near-enough. Measured on `a<b`, `a>b`, `a<=b`, `a>=b`, `a==b` and `a!=b` in one file:
            // at `space_around_relational_op = false` the oracle closes the first four up and
            // returns `a == b` and `a != b` spaced. There is no `space_around_equality_op` in the
            // export, in JetBrains' C# spaces schema or anywhere else — the gap around an equality
            // operator is not configurable, so it is written here rather than read from an option.
            _ => true
        };

    static bool IsAssignmentOperator(SyntaxToken token) =>
        token.Parent is AssignmentExpressionSyntax assignment
        && assignment.OperatorToken == token
        || token.IsKind(SyntaxKind.EqualsToken)
        && token.Parent is EqualsValueClauseSyntax or NameEqualsSyntax;

    /// <summary>
    ///     ⚠ True when omitting the space would let the two tokens lex as one.
    /// </summary>
    public static bool MustSeparate(SyntaxToken prev, SyntaxToken next) {
        var left = prev.Text;
        var right = next.Text;
        if (left.Length == 0 || right.Length == 0) {
            return false;
        }

        var a = left[^1];
        var b = right[0];

        if (IsWordChar(a) && IsWordChar(b)) {
            return true;
        }

        // ⚠ There is deliberately no rule here for `1.ToString()`. One stood in this spot for two
        // commits, forcing `1 .ToString()` on the stated ground that "without the space `1.` lexes
        // as the start of a real literal", and it was false: the C# lexer absorbs the dot into a
        // real literal only when a *digit* follows it, so `120.DegreesCelsius()`, `1.ToString()`,
        // `1.e5`, `1.f`, `1._5`, `1_000.Meters()` and `0x1F.ToString()` all tokenise as
        // NumericLiteral · Dot · Identifier — verified with Roslyn's own `ParseTokens`, and the
        // rewritten form compiles. The rule was *inserting* the space into correct code, and the
        // shape had never appeared in the conformance corpus, so nothing but its own unit test ever
        // measured it. ⚠ Even had the premise held, this is the wrong layer to defend it: the
        // token-stream promise (SK9099) refuses to write any output whose tokens differ from the
        // input's, so a genuine re-lex fails closed rather than reaching disk.

        // ⚠ `List<Dictionary<int, string>>` — the parser splits `>>` in a type context itself, so
        // forcing a space between two closing angles produces `int> >`, which is what a naive
        // "these characters combine" table does to nearly every generic signature in a real tree.
        if (IsTypeAngle(prev) && IsTypeAngle(next)) {
            return false;
        }

        return Combines(a, b);
    }

    static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '@' or '$';

    static bool Combines(char a, char b) =>
        (a, b) switch {
            ('+', '+')
                or ('-', '-')
                or ('+', '=')
                or ('-', '=')
                or ('*', '=')
                or ('/', '=')
                or ('%', '=')
                or ('&', '=')
                or ('|', '=')
                or ('^', '=')
                or ('!', '=')
                or ('=', '=')
                or ('<', '=')
                or ('>', '=')
                or ('=', '>')
                or ('-', '>')
                or ('&', '&')
                or ('|', '|')
                or ('<', '<')
                or ('>', '>')
                or (':', ':')
                or ('?', '?')
                or ('/', '/')
                or ('/', '*')
                or ('*', '/')
                or ('.', '.') => true,
            // ⚠ `?.` and `?[` are two tokens in C#, not one: writing them adjacent is what a
            // conditional access IS. Listing them here puts a space in every `a?.B` in a real tree.
            _ => false
        };
}
