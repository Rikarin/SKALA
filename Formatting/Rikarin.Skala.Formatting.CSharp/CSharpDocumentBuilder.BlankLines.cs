using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
///     The three-system blank-line resolution.
/// </summary>
/// <remarks>
///     docs/plan/04 § "Blank lines": <b>removals ∘ requirements ∘ caps</b>, evaluated on the gap
///     between two members and attributed to the member below — so that <c>stick_comment</c> moves the
///     right blank lines with the comment.
///     <para>
///         ⚠ This is the highest bug density in the whole formatter, because the three systems interact and
///         hand-reasoning about them is unreliable. It is verified against the oracle on
///         <c>constructs/blank-lines/</c>, not argued about.
///     </para>
/// </remarks>
public sealed partial class CSharpDocumentBuilder {
    int ResolveBlankLines(Piece previous, int nextPieceIndex, SyntaxToken nextToken, int sourceBlanks) {
        var declaration = nextToken.IsKind(SyntaxKind.None) || InDeclarationContext(nextToken);

        // 1. Caps. The author's runs are truncated, never extended.
        var cap = Math.Max(0, declaration ? _options.KeepBlankLinesInDeclarations : _options.KeepBlankLinesInCode);

        // 0. ⚠ Inside a `///` run the blank count is structure, and none of the three systems below
        // gets a vote. Roslyn ends a documentation comment at a blank line, so the gap between two
        // `///` lines is the only whitespace in the language where 0 → 1 *splits one trivia into
        // two* and 1 → 0 fuses two into one. Either is a changed token stream, the safety net
        // abandons the file, and `skala format` is a total outage on it — SK9099 rather than a
        // misplaced blank line.
        //
        // ⚠ Not hypothetical and not exotic (SK-FUZZ-0002). `stick_comment`'s early return below
        // spends a member's requirement on the gap *above* its comment rather than below it, but it
        // asks `previous.StartsLine` first — and the first `///` of a run that begins on the brace
        // line does not start a line:
        //
        //     interface I { /// <summary>x</summary>
        //       /// <remarks>y</remarks>
        //       int M();
        //
        // so the guard missed, `blank_lines_around_invocable` landed between the two `///` lines,
        // and the file could not be formatted at all. Move the run down one line and it formats
        // correctly, which is the whole tell: a token-stream outage that depends only on where the
        // run starts.
        //
        // ⚠ Only `///`. Two consecutive `//` lines are two separate trivia, so a blank between them
        // moves whitespace and nothing else.
        if (BetweenDocumentationLines(previous, nextPieceIndex)) {
            return sourceBlanks == 0 ? 0 : Math.Max(1, Math.Min(sourceBlanks, cap));
        }

        var blanks = Math.Min(sourceBlanks, cap);

        // 2. Requirements. A minimum inserted where absent.
        blanks = Math.Max(blanks, RequiredBlankLines(previous, nextPieceIndex, nextToken));

        // 3. Removals, which win over (2).
        if (RemovesNearBrace(previous, nextToken, declaration)) {
            blanks = 0;
        }

        return blanks;
    }

    /// <summary>
    /// Whether the gap runs between two lines of documentation comment.
    /// </summary>
    /// <remarks>
    /// ⚠ A blank line here is not spacing, it is the delimiter that ends a <c>///</c> run. See
    /// <see cref="ResolveBlankLines"/> for what putting one in the wrong place costs.
    /// </remarks>
    bool BetweenDocumentationLines(Piece previous, int nextPieceIndex) =>
        previous.Kind == PieceKind.DocCommentLine
        && nextPieceIndex >= 0
        && nextPieceIndex < _pieces.Length
        && _pieces[nextPieceIndex].Kind == PieceKind.DocCommentLine;

    bool RemovesNearBrace(Piece previous, SyntaxToken nextToken, bool declaration) {
        var enabled = declaration
            ? _options.RemoveBlankLinesNearBracesInDeclarations
            : _options.RemoveBlankLinesNearBracesInCode;
        if (!enabled) {
            return false;
        }

        if (previous.Kind == PieceKind.Token && _tokens[previous.TokenIndex].IsKind(SyntaxKind.OpenBraceToken)) {
            return true;
        }

        return nextToken.IsKind(SyntaxKind.CloseBraceToken);
    }

    int RequiredBlankLines(Piece previous, int nextPieceIndex, SyntaxToken nextToken) {
        var required = 0;

        // Regions get their blank lines whether or not there is a declaration on either side.
        if (nextPieceIndex >= 0
            && nextPieceIndex < _pieces.Length
            && _pieces[nextPieceIndex].Kind == PieceKind.RegionDirective
            || previous.Kind == PieceKind.RegionDirective) {
            required = Math.Max(required, RegionRequirement(previous, nextPieceIndex));
        }

        // ⚠ A gap that touches a conditional or a `#pragma` gets no requirement at all, and the
        // caps still apply to it. The requirement belongs to the boundary between two members and
        // not to whichever gap the directive happens to have landed in:
        // <code>
        // using System.Collections.Generic;
        // #if DNXCORE50            ← `blank_lines_after_using_list = 1` fired here
        // </code>
        // The oracle puts nothing there and the blank line after the matching `#endif` instead,
        // which is the same requirement paid at the boundary it is about. Measured at 156 lines
        // across 84 files of `corpus/real/` — one blank line per conditional region, and
        // Newtonsoft.Json is largely wrapped in them.
        if (TouchesDirective(previous, nextPieceIndex)) {
            return required;
        }

        // ⚠ stick_comment = true: a comment directly above a declaration is part of it, so the gap
        // BELOW the comment is inside the member and takes none of the member's requirement. The
        // requirement was already spent on the gap above the comment, which is the whole point of
        // "attributed to the member below" (docs/plan/04 § "Blank lines").
        if (_options.StickComment && previous.IsComment && previous.StartsLine) {
            return required;
        }

        // Statements, which the member rules below do not reach.
        if (previous.Kind == PieceKind.Token) {
            var previousToken = _tokens[previous.TokenIndex];

            // ⚠ blank_lines_after_block_statements: a statement that ended with a brace is
            // separated from the next one — and "ended with a brace" is not "is a block". Measured:
            // an `if … else { }`, a `switch { }` and a `try … catch { }` all get the blank line and
            // none of their closing braces belongs to a `BlockSyntax` whose parent is a statement.
            // The else's brace hangs from an `ElseClauseSyntax`, the switch's from the switch
            // itself, the catch's from a `CatchClauseSyntax`.
            // ⚠ And not before a `case`, which is a label rather than a statement: the gap between
            // a switch section's last block and the next label belongs to `blank_lines_before_case`,
            // which is 0 here, and the oracle leaves it empty.
            if (previousToken.IsKind(SyntaxKind.CloseBraceToken)
                && EndsAStatementInAList(previousToken)
                && nextToken.Parent is not SwitchLabelSyntax) {
                required = Math.Max(required, _options.BlankLinesAfterBlockStatements);
            }

            if (previousToken.Parent is SwitchLabelSyntax) {
                required = Math.Max(required, _options.BlankLinesAfterCase);
            }
        }

        if (!nextToken.IsKind(SyntaxKind.None)
            && nextToken.Parent is SwitchLabelSyntax
            && nextToken.Parent.Parent is SwitchSectionSyntax section
            && section.Parent is SwitchStatementSyntax owner
            && owner.Sections.IndexOf(section) > 0) {
            required = Math.Max(required, _options.BlankLinesBeforeCase);
        }

        if (nextPieceIndex >= 0
            && nextPieceIndex < _pieces.Length
            && _pieces[nextPieceIndex].Kind == PieceKind.LineComment
            && _pieces[nextPieceIndex].StartsLine) {
            required = Math.Max(required, _options.BlankLinesBeforeSingleLineComment);
        }

        var above = previous.Kind == PieceKind.Token ? MemberEndingAt(_tokens[previous.TokenIndex]) : null;
        var below = MemberStartingAt(nextPieceIndex, nextToken);

        if (above is null && below is null) {
            return required;
        }

        // ⚠ blank_lines_after_using_list applies to the boundary, not to either member's own rule.
        if (above is UsingDirectiveSyntax or ExternAliasDirectiveSyntax
            && below is not (UsingDirectiveSyntax or ExternAliasDirectiveSyntax)) {
            required = Math.Max(required, _options.BlankLinesAfterUsingList);
        }

        if (above is FileScopedNamespaceDeclarationSyntax
            || previous.Kind == PieceKind.Token
            && EndsFileScopedNamespaceDirective(_tokens[previous.TokenIndex])) {
            required = Math.Max(required, _options.BlankLinesAfterFileScopedNamespaceDirective);
        }

        if (above is not null) {
            required = Math.Max(required, RequirementFor(above));
        }

        if (below is not null) {
            required = Math.Max(required, RequirementFor(below));
        }

        return required;
    }

    /// <summary>
    ///     Whether either side of the gap is a conditional directive, a <c>#pragma</c> or disabled text.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not a region. <c>blank_lines_around_region</c> and <c>blank_lines_inside_region</c> are
    ///     requirements <em>about</em> the directive, and they are the only ones that are.
    /// </remarks>
    bool TouchesDirective(Piece previous, int nextPieceIndex) {
        if (IsDirective(previous.Kind)) {
            return true;
        }

        return nextPieceIndex >= 0 && nextPieceIndex < _pieces.Length && IsDirective(_pieces[nextPieceIndex].Kind);
    }

    static bool IsDirective(PieceKind kind) =>
        kind is PieceKind.ConditionalDirective or PieceKind.OtherDirective or PieceKind.DisabledText;

    int RegionRequirement(Piece previous, int nextPieceIndex) {
        var nextIsRegion = nextPieceIndex >= 0
            && nextPieceIndex < _pieces.Length
            && _pieces[nextPieceIndex].Kind == PieceKind.RegionDirective;
        var previousIsRegion = previous.Kind == PieceKind.RegionDirective;

        // Right inside the braces the removal rule wins anyway; elsewhere a region is separated
        // from the code around it.
        if (nextIsRegion && previousIsRegion) {
            return _options.BlankLinesInsideRegion;
        }

        // The gap just inside a region — after `#region`, before `#endregion` — is the region's
        // inside, not its outside.
        var opensBefore = previousIsRegion && previous.Text.StartsWith("#region", StringComparison.Ordinal);
        var closesAfter =
            nextIsRegion
            && _pieces[nextPieceIndex].Text.StartsWith("#endregion", StringComparison.Ordinal);

        return opensBefore || closesAfter ? _options.BlankLinesInsideRegion : _options.BlankLinesAroundRegion;
    }

    static bool EndsFileScopedNamespaceDirective(SyntaxToken token) =>
        token.IsKind(SyntaxKind.SemicolonToken) && token.Parent is FileScopedNamespaceDeclarationSyntax;

    /// <summary>The requirement one member states about the gaps on either side of it.</summary>
    int RequirementFor(SyntaxNode member) {
        var single = IsSingleLine(member);
        return member switch {
            NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax => _options.BlankLinesAroundNamespace,
            BaseTypeDeclarationSyntax or DelegateDeclarationSyntax =>
                single ? _options.BlankLinesAroundSingleLineType : _options.BlankLinesAroundType,
            MethodDeclarationSyntax
                or ConstructorDeclarationSyntax
                or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax
                or ConversionOperatorDeclarationSyntax =>
                single ? _options.BlankLinesAroundSingleLineInvocable : _options.BlankLinesAroundInvocable,
            LocalFunctionStatementSyntax =>
                single ? _options.BlankLinesAroundSingleLineLocalMethod : _options.BlankLinesAroundLocalMethod,
            PropertyDeclarationSyntax property => IsAutoProperty(property)
                ? single ? _options.BlankLinesAroundSingleLineAutoProperty : _options.BlankLinesAroundAutoProperty
                : single ? _options.BlankLinesAroundSingleLineProperty : _options.BlankLinesAroundProperty,
            IndexerDeclarationSyntax or EventDeclarationSyntax =>
                single ? _options.BlankLinesAroundSingleLineProperty : _options.BlankLinesAroundProperty,
            FieldDeclarationSyntax or EventFieldDeclarationSyntax =>
                single ? _options.BlankLinesAroundSingleLineField : _options.BlankLinesAroundField,
            AccessorDeclarationSyntax =>
                single ? _options.BlankLinesAroundSingleLineAccessor : _options.BlankLinesAroundAccessor,
            _ => 0
        };
    }

    static bool IsAutoProperty(PropertyDeclarationSyntax property) =>
        property.AccessorList is { } accessors
        && accessors.Accessors.All(static a => a.Body is null && a.ExpressionBody is null);

    /// <summary>
    ///     One line including whatever <c>stick_comment</c> attached to it: a method with a doc comment
    ///     is not single-line even when its body is, which is what docs/plan/05 § "Blank lines" means
    ///     by "M() has a doc comment and is not single-line".
    /// </summary>
    /// <remarks>
    ///     ⚠ "One line" is a property of the <em>output</em>, and reading it off the input is a
    ///     non-idempotency as soon as the formatter can break a line. A 140-column field is single-line
    ///     in the source, gets chopped into four, and then wants the blank line that
    ///     <c>blank_lines_around_single_line_field = 0</c> had just declined to give it — so the first
    ///     pass produces one shape and the second produces another. Milestone 1 never chopped anything,
    ///     so the question never arose; it took a run over Skala's own source to find, because no corpus
    ///     file has a member long enough.
    /// </remarks>
    bool IsSingleLine(SyntaxNode member) {
        var start = _options.StickComment ? StickyStart(member) : member.Span.Start;
        var lines = _text.Lines;
        var first = lines.GetLineFromPosition(start);
        if (first.LineNumber != lines.GetLineFromPosition(member.Span.End).LineNumber) {
            return false;
        }

        // ⚠ From *after* the member's first token, not from its start. The break that puts a member
        // on a line of its own sits exactly at the member's first character, and counting it as a
        // break "inside" the member makes every member multi-line at once — which reads as a
        // blank-line bug rather than as a measurement one: adjacent single-line fields suddenly take
        // `blank_lines_around_field = 1` instead of `blank_lines_around_single_line_field = 0`.
        if (_plan.HasForcedBreakIn(member.Span.Start + 1, member.Span.End)) {
            return false;
        }

        // ⚠ Milestone 2 also declined here for a member that shared its line with the member above
        // it, because such a member had no stable notion of "single line" — its width test is
        // against column 0 while the fitter met it half way along a line. That guard is gone,
        // because the case it guarded against is gone: BreakPlan.PlanOnePerLine gives every member a
        // line of its own, so the question is now asked about the shape the output will have. The
        // guard outliving the case is what put a blank line between `int B => 2;` and `int C => 3;`
        // that the oracle does not write.

        // A line the fitter will chop is not a line, so the width has to be the one the fitter will
        // see. ⚠ Not the source line's width: the member's own span plus the indentation the OUTPUT
        // gives it. Reading the source's leading whitespace instead makes the answer depend on the
        // author's indentation, and `format(mutate_indentation(x)) ≡ format(x)` is a property the
        // suite asserts on every corpus file.
        //
        // ⚠ And not the source's *interior* whitespace either, which is the same mistake one step
        // in and took a fuzzer to find (SK-FUZZ-0004's sibling, SK-FUZZ-0007). `TextWidth.Measure`
        // over the member's span counts the gaps the author wrote between its tokens — gaps this
        // formatter is about to collapse to one column or to none. `void M(int a);` and
        // `void M(int<108 spaces>a);` are the same member and the same output, and the second was
        // measured at 122 columns, called multi-line, and given the blank line above it that
        // `blank_lines_around_single_line_invocable = 0` had just declined to give the first. Two
        // inputs differing in one inter-token gap produced outputs differing by a whole line, on a
        // line the formatter rewrites anyway. docs/plan/16 § R2 is about exactly this: the fitter
        // is the novel code, and the property suite is what contains its risk.
        var width = OutputIndentColumns(member) + OutputWidth(member);
        return width <= _options.MaxLineLength;
    }

    /// <summary>
    /// The columns the member's own text will occupy once the formatter has respaced it.
    /// </summary>
    /// <remarks>
    /// ⚠ A function of the token stream, never of the gaps between the tokens in the source. That
    /// is the whole point: the caller is deciding a blank line, and a decision that reads whitespace
    /// the formatter is about to rewrite is not absorbed by <c>format(mutate_whitespace(x)) ≡
    /// format(x)</c>.
    /// <para>
    /// The one place the source is still consulted is <see cref="SpaceKind.Preserve"/>, and there it
    /// is correct rather than tolerated: an ungoverned gap is one <c>extra_spaces = remove_all</c>
    /// collapses to whatever the author had — one space or none — so the output really does carry
    /// that bit, and reading it is reading the output. Widening such a gap does not change the
    /// answer, because a run and a single space collapse alike.
    /// </para>
    /// <para>
    /// ⚠ Only ever called for a member <see cref="IsSingleLine"/> has already found on one source
    /// line, so no token here spans lines and <see cref="TextWidth.Measure"/>'s newline reset cannot
    /// be reached.
    /// </para>
    /// </remarks>
    int OutputWidth(SyntaxNode member) {
        var width = 0;
        var previous = default(SyntaxToken);
        var budget = _options.MaxLineLength;

        foreach (var token in member.DescendantTokens()) {
            // ⚠ Zero-width tokens are not pieces and are not written; see EmitToken.
            if (token.Span.Length == 0) {
                continue;
            }

            if (!previous.IsKind(SyntaxKind.None)) {
                width += GapWidth(previous, token);
            }

            width += TextWidth.Measure(token.Text);
            previous = token;

            // Nothing above this call cares how far past the margin a member is, only that it is.
            if (width > budget) {
                return width;
            }
        }

        return width;
    }

    /// <summary>The columns the formatter will write between two adjacent tokens of a member.</summary>
    int GapWidth(SyntaxToken previous, SyntaxToken next) {
        // A comment in the gap is written out, with `space_before_trailing_comment` on each side of
        // it — the same answer GapSpace gives, which resolves a gap whose neighbour is not a token
        // to Required.
        var width = 0;
        var comments = false;
        foreach (var trivia in previous.TrailingTrivia) {
            Measure(trivia, ref width, ref comments);
        }

        foreach (var trivia in next.LeadingTrivia) {
            Measure(trivia, ref width, ref comments);
        }

        if (comments) {
            return width + (_options.SpaceBeforeTrailingComment ? 1 : 0);
        }

        var kind = SpaceRules.Decide(previous, next, _options);
        if (kind == SpaceKind.Preserve) {
            kind = HasSpace(previous.Span.End, next.SpanStart) ? SpaceKind.Required : SpaceKind.Forbidden;
        }

        return kind == SpaceKind.Forbidden ? 0 : 1;
    }

    void Measure(SyntaxTrivia trivia, ref int width, ref bool comments) {
        if (trivia.Kind() is not (SyntaxKind.SingleLineCommentTrivia
                or SyntaxKind.MultiLineCommentTrivia
                or SyntaxKind.SingleLineDocumentationCommentTrivia
                or SyntaxKind.MultiLineDocumentationCommentTrivia)) {
            return;
        }

        width += (_options.SpaceBeforeTrailingComment ? 1 : 0) + TextWidth.Measure(trivia.ToString());
        comments = true;
    }

    /// <summary>The column a member will start at, counted from the tree rather than from the text.</summary>
    int OutputIndentColumns(SyntaxNode member) {
        var level = 0;
        for (var node = member.Parent; node is not null; node = node.Parent) {
            switch (node) {
                case BlockSyntax:
                case BaseTypeDeclarationSyntax:
                case AccessorListSyntax:
                case SwitchSectionSyntax:
                    level++;
                    break;

                case NamespaceDeclarationSyntax when _options.IndentInsideNamespace:
                    level++;
                    break;

                case SwitchStatementSyntax when _options.IndentSwitchLabels:
                    level++;
                    break;

                default:
                    break;
            }
        }

        return level * _options.IndentSize;
    }

    /// <summary>
    ///     Where a member starts once the comment block directly above it is counted as part of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Only for <see cref="IsSingleLine" />. <c>stick_comment = true</c> still moves a plain
    ///     comment with its member for the purposes of the blank-line <em>gap</em>; what this answers is
    ///     the narrower question of whether the member counts as occupying one line.
    /// </remarks>
    static int StickyStart(SyntaxNode member) {
        var start = member.Span.Start;
        foreach (var trivia in member.GetLeadingTrivia().Reverse()) {
            switch (trivia.Kind()) {
                case SyntaxKind.WhitespaceTrivia:
                    continue;

                case SyntaxKind.EndOfLineTrivia:
                    // A blank line breaks the stick; one newline between the comment and the member
                    // does not.
                    continue;

                // ⚠ A *documentation* comment joins the member for the purpose of "is this member
                // single line"; a plain comment does not, and the two are two lines apart in the
                // oracle's output:
                //     // A plain comment above a one-line property.
                //     public int A => 1;
                //     public int B => 2;      ← no blank line: A is single-line
                //
                //     /// <summary>A doc comment.</summary>
                //     public int C => 3;
                //                             ← a blank line: C is not
                //     public int D => 4;
                // docs/plan/05 § "Blank lines" says exactly this — "M() has a doc comment and is not
                // single-line" — and milestone 1 read it as being about comments in general.
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    start = trivia.FullSpan.Start;
                    continue;

                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                    continue;

                default:
                    return start;
            }
        }

        return start;
    }

    /// <summary>The outermost declaration whose last token is <paramref name="token" />.</summary>
    static SyntaxNode? MemberEndingAt(SyntaxToken token) {
        SyntaxNode? found = null;
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.Span.End != token.Span.End) {
                break;
            }

            if (IsBlankLineSubject(node)) {
                found = node;
            }
        }

        return found;
    }

    /// <summary>
    ///     The outermost declaration that starts at the next piece — the comment above it included, so
    ///     the gap is attributed to the member below rather than to the comment.
    /// </summary>
    SyntaxNode? MemberStartingAt(int nextPieceIndex, SyntaxToken nextToken) {
        var token = nextToken;
        if (token.IsKind(SyntaxKind.None) && nextPieceIndex >= 0) {
            // The next piece is a comment or a directive: look through it to the code it precedes,
            // which is what stick_comment asks for.
            for (var i = nextPieceIndex; i < _pieces.Length; i++) {
                if (_pieces[i].Kind == PieceKind.Token) {
                    token = _tokens[_pieces[i].TokenIndex];
                    break;
                }

                if (!_pieces[i].IsComment) {
                    return null;
                }
            }
        }

        if (token.IsKind(SyntaxKind.None)) {
            return null;
        }

        SyntaxNode? found = null;
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.Span.Start != token.Span.Start) {
                break;
            }

            if (IsBlankLineSubject(node)) {
                found = node;
            }
        }

        return found;
    }

    static bool IsBlankLineSubject(SyntaxNode node) =>
        node is MemberDeclarationSyntax
            or AccessorDeclarationSyntax
            or UsingDirectiveSyntax
            or ExternAliasDirectiveSyntax
            or LocalFunctionStatementSyntax;

    /// <summary>
    ///     Declarations or code? The caps differ (<c>keep_blank_lines_in_declarations</c> against
    ///     <c>keep_blank_lines_in_code</c>) and so do the removal keys.
    /// </summary>
    static bool InDeclarationContext(SyntaxToken token) {
        for (var node = token.Parent; node is not null; node = node.Parent) {
            switch (node) {
                case BlockSyntax:
                case StatementSyntax:
                case InitializerExpressionSyntax:
                    return false;

                case BaseTypeDeclarationSyntax:
                case NamespaceDeclarationSyntax:
                case FileScopedNamespaceDeclarationSyntax:
                case CompilationUnitSyntax:
                case AccessorListSyntax:
                    return true;

                default:
                    continue;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether this token is the last one of a statement that is itself an element of a statement
    ///     list.
    /// </summary>
    /// <remarks>
    ///     ⚠ The "in a list" half is not decoration. <see cref="BlockSyntax" /> <em>is</em> a
    ///     <see cref="StatementSyntax" />, so a method body's own closing brace ends a statement by the
    ///     first half of the test alone, and the rule would put a blank line between every method's
    ///     last brace and whatever follows it.
    ///     <para>
    ///         ⚠ And the walk does not stop at the first statement it meets, which is the other half.
    ///         An <c>else</c>'s block is a statement whose parent is an <see cref="ElseClauseSyntax" />, and
    ///         a <c>foreach</c>'s block is one whose parent is the loop — neither is in a list, and the
    ///         statement that is lies one or two nodes further up.
    ///     </para>
    /// </remarks>
    static bool EndsAStatementInAList(SyntaxToken token) {
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.Span.End != token.Span.End) {
                return false;
            }

            if (node is StatementSyntax && node.Parent is BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax) {
                return true;
            }
        }

        return false;
    }
}
