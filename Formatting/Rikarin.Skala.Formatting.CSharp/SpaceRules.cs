using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// One inter-token gap resolved to Required or Forbidden.
/// </summary>
/// <remarks>
/// docs/plan/05 § "Spaces": ninety keys, each of which resolves one gap. <c>extra_spaces =
/// remove_all</c> is the global backstop — any run of spaces not required by a rule collapses to
/// one or to none — which is why this function is total: there is no "leave it alone" answer.
/// <para>
/// ⚠ <see cref="MustSeparate"/> overrides everything. A Forbidden gap between two tokens that would
/// lex as one produces a corrupted file. The safety net would catch it and abandon the file, which
/// is correct behaviour for a bug but is still a bug; this is the place not to make it.
/// </para>
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
    /// The gaps no rule in the export governs, where the oracle leaves whatever the author wrote.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="SpaceKind.Preserve"/> exists in the IR from milestone 1 and nothing produced it
    /// until now, which made this class total in the wrong way: every gap got an answer and two of
    /// them were answers the oracle does not give. Asked directly, <c>[1, ..a]</c> comes back
    /// <c>..a</c> and <c>[1, ..   a]</c> comes back <c>.. a</c>; <c>a[1..3]</c> stays closed up and
    /// <c>a[1  ..  3]</c> comes back <c>a[1 .. 3]</c>. That is not a rule with a value, it is
    /// <c>extra_spaces = remove_all</c> collapsing a run in a gap nobody legislated.
    /// <para>
    /// ⚠ A slice pattern is <em>not</em> in this set and looks as though it should be:
    /// <c>a is [1, ..var r]</c> comes back <c>.. var r</c>, a space the oracle inserts, because
    /// <c>space_within_slice_pattern = true</c> really does govern that one. Reading
    /// <c>space_within_spread_pattern</c> as the collection-expression twin of it — which is what
    /// its name says — is what put a space Skala had no evidence for into 58 lines of
    /// <c>corpus/real/</c>.
    /// </para>
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
            if (left == SyntaxKind.OpenParenToken && right == SyntaxKind.CloseParenToken) {
                return false;
            }

            var owner = left == SyntaxKind.OpenParenToken ? prev.Parent : next.Parent;
            return owner is CastExpressionSyntax ? o.SpaceBetweenTypecastParentheses : o.SpaceWithinParentheses;
        }

        // ── Brackets ─────────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.OpenBracketToken) {
            return BeforeOpenBracket(prev, next, o);
        }

        if (left == SyntaxKind.OpenBracketToken) {
            return WithinBrackets(prev.Parent, o) && right != SyntaxKind.CloseBracketToken;
        }

        if (right == SyntaxKind.CloseBracketToken) {
            return WithinBrackets(next.Parent, o) && left != SyntaxKind.OpenBracketToken;
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

            // `{ get; set; }` — the gap between two accessors written on one line.
            return prev.Parent is AccessorDeclarationSyntax { Parent: AccessorListSyntax }
                ? o.SpaceBetweenAccessorsInSinglelineProperty
                : true;
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
        if (IsMemberAccessPunctuation(prev) || IsMemberAccessPunctuation(next)) {
            return o.SpaceAroundMemberAccess;
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

        // ── Colons ───────────────────────────────────────────────────────────────────────────
        if (right == SyntaxKind.ColonToken) {
            return next.Parent switch {
                BaseListSyntax => o.SpaceBeforeColonInInheritance,
                TypeParameterConstraintClauseSyntax => o.SpaceBeforeTypeParameterConstraintColon,
                ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax => o.SpaceBeforeColonInCtorInitializer,
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
            return o.SpaceAfterUnaryOperator;
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
    /// The gap around a <c>..</c>. ⚠ A prefix range with no left operand — which is how Roslyn
    /// parses a spread inside an array initializer — is a spread, not a range, and gets the space.
    /// </summary>
    /// <summary>
    /// The gap beside a <c>..</c> that a rule really does govern: a slice pattern's.
    /// </summary>
    /// <remarks>
    /// ⚠ A collection expression's spread element used to be answered here too, out of
    /// <c>space_within_spread_pattern</c>. It is not governed at all — see <see cref="Ungoverned"/>
    /// — and the key is inert at both values.
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
            return o.SpaceAfterUnaryOperator;
        }

        switch (prev.Kind()) {
            case SyntaxKind.IfKeyword:
            case SyntaxKind.WhileKeyword:
            case SyntaxKind.ForKeyword:
            case SyntaxKind.ForEachKeyword:
            case SyntaxKind.SwitchKeyword:
            case SyntaxKind.CatchKeyword:
            case SyntaxKind.LockKeyword:
            case SyntaxKind.UsingKeyword:
            case SyntaxKind.FixedKeyword:
                return o.SpaceAfterKeywordsInControlFlow;

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

            default:
                break;
        }

        if (prev.Text is "nameof") {
            return o.SpaceBeforeNameofParentheses;
        }

        var empty = next.Parent is BaseArgumentListSyntax { Arguments.Count: 0 }
            or BaseParameterListSyntax { Parameters.Count: 0 };

        switch (next.Parent) {
            case ParameterListSyntax { Parent: ParenthesizedLambdaExpressionSyntax } :
                // ⚠ A lambda's parentheses are the head of an operand, not a call site: whatever
                // precedes decides. `x += (a, b) => …` needs its space; `M((a, b) => …)` does not.
                return !ClingsRight(prev.Kind()) && !IsCallSite(prev);

            case ParameterListSyntax or FunctionPointerParameterListSyntax:
                return empty ? o.SpaceBeforeEmptyMethodParentheses : o.SpaceBeforeMethodParentheses;

            case ArgumentListSyntax {
                Parent: ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
            } :
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
    /// True when <paramref name="prev"/> would make the following <c>(</c> read as a call.
    /// </summary>
    /// <remarks>
    /// ⚠ A <c>&gt;</c> qualifies only when it closes a type argument list — <c>Foo&lt;int&gt;(x)</c>
    /// is a call and <c>count &gt; (buffer.Length - index)</c> is a comparison. Treating every
    /// <c>&gt;</c> as a call site removed the space after the operator and produced
    /// <c>count &gt;(buffer.Length - index)</c>. It survived milestone 3 because every corpus line
    /// that shows it sits inside a <c>#if</c> body, which the formatter could not see until M5
    /// supplied preprocessor symbols — the symbols did not cause the bug, they revealed it.
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
                ArrayRankSpecifierSyntax => o.SpaceBeforeArrayRankBrackets,
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
                _ => o.SpaceBeforeOpenSquareBrackets
            };

    static bool WithinBrackets(SyntaxNode? owner, in PhaseOneOptions o) =>
        owner switch {
            AttributeListSyntax => o.SpaceWithinAttributeBrackets,
            ListPatternSyntax => o.SpaceWithinListPatternBrackets,
            BracketedArgumentListSyntax or ImplicitElementAccessSyntax => o.SpaceWithinArrayAccessBrackets,
            CollectionExpressionSyntax => o.SpaceWithinSlicePattern && false,
            _ => false
        };

    static bool WithinAngles(SyntaxNode? owner, in PhaseOneOptions o) =>
        owner is TypeParameterListSyntax ? o.SpaceWithinTypeParameterAngles : o.SpaceWithinTypeArgumentAngles;

    static bool BeforeOpenBrace(SyntaxToken prev, SyntaxToken next, in PhaseOneOptions o) =>
        next.Parent switch {
            AccessorListSyntax => o.SpaceBeforeSinglelineAccessorholder,
            _ => !ClingsRight(prev.Kind())
        };

    /// <summary>The gap just inside a brace, on whichever side.</summary>
    static bool WithinBraces(SyntaxNode? owner, SyntaxToken other, in PhaseOneOptions o) {
        // `{ }` — empty_block_style = together with space_within_empty_braces = true.
        if (other.Kind() is SyntaxKind.CloseBraceToken or SyntaxKind.OpenBraceToken) {
            return o.SpaceWithinEmptyBraces;
        }

        return owner switch {
            AccessorListSyntax => o.SpaceInSinglelineAccessorholder,
            BlockSyntax {
                Parent: AnonymousMethodExpressionSyntax
                    or SimpleLambdaExpressionSyntax
                    or ParenthesizedLambdaExpressionSyntax
            } =>
                o.SpaceInSinglelineAnonymousMethod,
            BlockSyntax {
                Parent: BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax
            } =>
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

    static bool IsPrefixOperator(SyntaxToken token) =>
        token.Parent is PrefixUnaryExpressionSyntax prefix && prefix.OperatorToken == token;

    static bool IsPostfixOperator(SyntaxToken token) =>
        token.Parent is PostfixUnaryExpressionSyntax postfix && postfix.OperatorToken == token;

    /// <summary>
    /// The <c>*</c> of a pointer type, <c>delegate*</c>'s included.
    /// </summary>
    /// <remarks>
    /// ⚠ A function pointer's asterisk hangs from <see cref="FunctionPointerTypeSyntax"/> rather
    /// than from a <see cref="PointerTypeSyntax"/>, so it fell through to the operator rules and
    /// came back as a multiplication: <c>readonly delegate * unmanaged &lt; nint, nint &gt; f;</c>.
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
                or SyntaxKind.GreaterThanEqualsToken
                or SyntaxKind.EqualsEqualsToken
                or SyntaxKind.ExclamationEqualsToken => o.SpaceAroundRelationalOp,
            _ => true
        };

    static bool IsAssignmentOperator(SyntaxToken token) =>
        token.Parent is AssignmentExpressionSyntax assignment
        && assignment.OperatorToken == token
        || token.IsKind(SyntaxKind.EqualsToken)
        && token.Parent is EqualsValueClauseSyntax or NameEqualsSyntax;

    /// <summary>
    /// ⚠ True when omitting the space would let the two tokens lex as one.
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
