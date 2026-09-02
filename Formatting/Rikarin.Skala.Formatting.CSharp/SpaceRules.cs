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
    /// </remarks>
    static bool Ungoverned(SyntaxToken prev, SyntaxToken next) =>
        prev.IsKind(SyntaxKind.DotDotToken)
            ? prev.Parent is SpreadElementSyntax or RangeExpressionSyntax { RightOperand: not null }
            : next.IsKind(SyntaxKind.DotDotToken)
            && next.Parent is RangeExpressionSyntax { LeftOperand: not null };

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
        if (right == SyntaxKind.ColonToken) {
            return next.Parent switch {
                BaseListSyntax => o.SpaceBeforeColonInInheritance,
                TypeParameterConstraintClauseSyntax => o.SpaceBeforeTypeParameterConstraintColon,
                // ⚠ Not `space_before_colon_in_ctor_initializer`, which the C# formatter does not
                // read. Measured on `public C() : base(1)` beside `public C(int a): this()` — one
                // written with the space and one without, so the probe could see an insertion and a
                // removal — one key flipped at a time over the export: at `true` and at `false`
                // alike the oracle returns both as ` : `. The key is in ReSharper's export beside
                // `space_before_colon_in_bitfield_declarator` and the rest of the C++ colon family;
                // C# spends one space here whatever it says.
                ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax => true,
                SwitchLabelSyntax => o.SpaceBeforeColonInCase,
                ConditionalExpressionSyntax => o.SpaceBeforeTernaryColon,
                _ => false
            };
        }

        if (left == SyntaxKind.ColonToken) {
            return prev.Parent switch {
                BaseListSyntax => o.SpaceAfterColonInInheritance,
                TypeParameterConstraintClauseSyntax => o.SpaceAfterTypeParameterConstraintColon,
                ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax => true,
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
                // ⚠ `var (a, b) = …` and `is Point (1, 2)`: an identifier precedes a parenthesis and
                // it is not a call. Without this the deconstruction reads `var(a, b)`, which is a
                // shape that appears in every modern C# tree.
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
        // `{ }` — empty_block_style = together with space_within_empty_braces = true.
        if (other.Kind() is SyntaxKind.CloseBraceToken or SyntaxKind.OpenBraceToken) {
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
            InitializerExpressionSyntax initializer =>
                initializer.IsKind(SyntaxKind.ArrayInitializerExpression)
                || initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
                    ? o.SpaceWithinSingleLineArrayInitializerBraces
                    : true,
            _ => true
        };
    }

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

        // `1 .ToString()`: without the space `1.` lexes as the start of a numeric literal.
        // ⚠ Only for an actual numeric literal. Testing the last character alone puts a space in
        // `v2.Count`, which is one of the most common shapes in any real tree.
        if (prev.IsKind(SyntaxKind.NumericLiteralToken) && b == '.' && !next.IsKind(SyntaxKind.DotDotToken)) {
            return true;
        }

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
