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
    /// <summary>
    ///     The removal-suppressing half of the family, wrapped around the resolution itself.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>disable_line_break_removal</c> is one-directional, and a blank run is where the two
    ///     directions are easiest to conflate. Two of the three systems below only ever
    ///     <em>reduce</em> a run — the cap and the near-brace removal — and one only ever raises it.
    ///     Clamping the resolved count up to the author's own is exactly "the reductions are off and
    ///     the requirement is not": measured, the oracle kept a three-blank run the cap would have cut
    ///     to two and still inserted the blank <c>blank_lines_around_invocable</c> asks for, on the
    ///     same file where <c>disable_blank_line_changes</c> suppressed both. SK-DIV-0064.
    /// </remarks>
    int ResolveBlankLines(Piece previous, int nextPieceIndex, SyntaxToken nextToken, int sourceBlanks) {
        var blanks = ResolveBlankLinesCore(previous, nextPieceIndex, nextToken, sourceBlanks);
        return _options.DisableLineBreakRemoval ? Math.Max(blanks, sourceBlanks) : blanks;
    }

    int ResolveBlankLinesCore(Piece previous, int nextPieceIndex, SyntaxToken nextToken, int sourceBlanks) {
        // ⚠ `disable_blank_line_changes = true` short-circuits all three systems at once, which is
        // the whole reason this method is the one funnel: caps, requirements and removals are the
        // only three things in the formatter that can change a blank-line count, so returning the
        // author's own count here is the complete implementation rather than the first of several
        // sites. Measured — the oracle neither truncates a run past the cap nor inserts a required
        // blank, and still adds and removes line breaks that are not blank lines (SK-DIV-0060).
        //
        // ⚠ It also subsumes the documentation-comment guard below rather than skipping it: a `///`
        // run's own count is what comes back, so the 0 → 1 split that guard exists to prevent cannot
        // happen on this path either.
        //
        // ⚠ `disable_line_break_changes` arrives at the same return, and that is the measurement
        // rather than an economy: the oracle's answer to it includes "blank runs included", so it is
        // the union of this key and `disable_line_break_removal` and not a third rule (SK-DIV-0063).
        if (_options.DisableBlankLineChanges || _options.DisableLineBreakChanges) {
            return sourceBlanks;
        }

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

        // 4. ⚠ One requirement outranks the removal, and outranks the cap as well.
        // `blank_lines_inside_type` and `blank_lines_inside_namespace` are requirements about the
        // *brace* rather than about the member below it, so the gap they govern is exactly the gap
        // `remove_blank_lines_near_braces_in_declarations` empties. Ordered like the others they
        // could never be observed at all, and that is why both keys were `OfInert` with a reason
        // that only held at the export's own `0`.
        //
        // Measured rather than assumed, at `jb cleanupcode` 2025.2.6 under this repository's
        // .editorconfig — which sets `remove_blank_lines_near_braces_in_declarations = true` and
        // `keep_blank_lines_in_declarations = 2`:
        //
        //     blank_lines_inside_type = 3   →  three blank lines after `{` and before `}`
        //     blank_lines_inside_type = 5   →  five, so the cap of 2 does not bind either
        //
        // Both keys are `0` in the export, so at this repository's configuration `Math.Max` with
        // zero is the identity and nothing moves.
        return Math.Max(blanks, InsideDeclarationBraces(previous, nextToken));
    }

    /// <summary>
    ///     <c>blank_lines_inside_type</c> and <c>blank_lines_inside_namespace</c>: the gap directly
    ///     after a declaration's <c>{</c> and directly before its <c>}</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Which bodies have an "inside" was measured, not read off the option's name. The oracle
    ///     pads a <c>class</c>, <c>struct</c>, <c>interface</c>, <c>record</c> and <c>enum</c> body —
    ///     <see cref="BaseTypeDeclarationSyntax" />, so an enum counts and the name "type" is honest —
    ///     and pads a nested type's braces as well as a top-level one's. It does <em>not</em> pad a
    ///     method body, an accessor list or an <c>if</c> block, all of which sit inside a type and none
    ///     of which is the type's own brace.
    ///     <para>
    ///         ⚠ A file-scoped namespace gets nothing, and that is the tool's answer rather than an
    ///         omission here: <c>namespace Fs;</c> has no braces, so it has no inside to pad, and the
    ///         probe at <c>blank_lines_inside_namespace = 2</c> returned it byte-identical.
    ///     </para>
    /// </remarks>
    int InsideDeclarationBraces(Piece previous, SyntaxToken nextToken) {
        if (previous.Kind == PieceKind.Token) {
            var open = _tokens[previous.TokenIndex];
            if (open.IsKind(SyntaxKind.OpenBraceToken) && Requirement(open) is { } after) {
                return after;
            }
        }

        return nextToken.IsKind(SyntaxKind.CloseBraceToken) && Requirement(nextToken) is { } before ? before : 0;

        int? Requirement(SyntaxToken brace) =>
            brace.Parent switch {
                BaseTypeDeclarationSyntax => _options.BlankLinesInsideType,
                NamespaceDeclarationSyntax => _options.BlankLinesInsideNamespace,
                _ => null
            };
    }

    /// <summary>
    ///     Whether the gap runs between two lines of documentation comment.
    /// </summary>
    /// <remarks>
    ///     ⚠ A blank line here is not spacing, it is the delimiter that ends a <c>///</c> run. See
    ///     <see cref="ResolveBlankLines" /> for what putting one in the wrong place costs.
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

        // ⚠ blank_lines_after_start_comment, and it has to be asked *above* the stick_comment return
        // rather than below it. A file header comment is a comment directly above a declaration, so
        // stick_comment's rule — "the gap below the comment is inside the member and takes none of
        // its requirement" — is exactly right about the member's own requirement and exactly wrong
        // about this one, which is a requirement about the comment. Asked below the return it never
        // fires: `constructs/blank-lines/a-file-header-comment*.cs` are the two fixtures that say so,
        // and both were failing before this rule existed.
        if (IsStartComment(previous)) {
            required = Math.Max(required, _options.BlankLinesAfterStartComment);
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

            // The two "after" statement requirements, attributed to the statement that ends here.
            if (StatementEndingAt(previousToken) is { } statementAbove) {
                if (IsControlTransfer(statementAbove)) {
                    required = Math.Max(required, _options.BlankLinesAfterControlTransferStatements);
                }

                if (SpansLines(statementAbove)) {
                    required = Math.Max(required, _options.BlankLinesAfterMultilineStatements);
                }
            }
        }

        if (StatementStartingAt(nextToken) is { } statementBelow) {
            if (IsControlTransfer(statementBelow)) {
                required = Math.Max(required, _options.BlankLinesBeforeControlTransferStatements);
            }

            if (SpansLines(statementBelow)) {
                required = Math.Max(required, _options.BlankLinesBeforeMultilineStatements);
            }

            if (HasChildBlock(statementBelow)) {
                required = Math.Max(required, _options.BlankLinesBeforeBlockStatements);
            }
        }

        if (!nextToken.IsKind(SyntaxKind.None)
            && nextToken.Parent is SwitchLabelSyntax
            && nextToken.Parent.Parent is SwitchSectionSyntax section
            && section.Parent is SwitchStatementSyntax owner
            && owner.Sections.IndexOf(section) is var index and > 0) {
            required = Math.Max(required, _options.BlankLinesBeforeCase);
            required = Math.Max(required, SectionRequirement(section));
            required = Math.Max(required, SectionRequirement(owner.Sections[index - 1]));
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

    /// <summary>
    ///     Whether a statement will occupy more than one line of the <b>output</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not <see cref="IsSingleLine" /> negated, and the difference is the whole of why this
    ///     exists. That test opens by asking whether the member spans more than one source line,
    ///     which is a fair approximation for a member and a wrong answer for a statement:
    ///     <code>
    /// var z = new[] {
    ///     1, 2, 3
    /// };
    ///     </code>
    ///     is three source lines and one output line — <c>place_simple_initializer_on_single_line</c>
    ///     joins it — and reading the source there made
    ///     <c>blank_lines_after_multiline_statements = 1</c> write a blank the oracle does not.
    ///     <para>
    ///         ⚠ So the question is asked of the plan instead, in the same terms the emitter asks it:
    ///         a source break survives unless its gap is <see cref="GapRule.Flat" /> or belongs to a group
    ///         the plan already fixed at <see cref="GroupMode.Flat" />. A gap the plan does not govern —
    ///         one with a comment in it — always survives, because the emitter never joins across a
    ///         comment.
    ///     </para>
    ///     <para>
    ///         ⚠ <see cref="GroupMode.Auto" /> is counted as breaking, which is the one guess here. The
    ///         fitter has not run, so nobody can know; the width test below is the same question asked
    ///         crudely, and an Auto group that fits is one this returns true for and the oracle may not.
    ///         No shape probed reaches it — <c>Auto</c> groups in this export are the wrapping styles, whose
    ///         own keys are another agent's — and it is recorded rather than hidden.
    ///     </para>
    /// </remarks>
    bool SpansLines(SyntaxNode node) {
        var previous = default(SyntaxToken);
        foreach (var token in node.DescendantTokens()) {
            if (token.Span.Length == 0) {
                continue;
            }

            // A verbatim or raw string is a single token that is itself several lines.
            if (token.Text.Contains('\n', StringComparison.Ordinal)) {
                return true;
            }

            if (!previous.IsKind(SyntaxKind.None) && GapBreaks(previous, token)) {
                return true;
            }

            previous = token;
        }

        return _plan.HasForcedBreakIn(node.Span.Start + 1, node.Span.End)
            || OutputIndentColumns(node) + OutputWidth(node) > _options.MaxLineLength;
    }

    /// <summary>Whether the gap between two adjacent tokens will hold a line break.</summary>
    bool GapBreaks(SyntaxToken previous, SyntaxToken next) {
        var newLine = false;
        var comment = false;
        for (var i = previous.Span.End; i < next.SpanStart; i++) {
            if (_source[i] == '\n') {
                newLine = true;
            } else if (!char.IsWhiteSpace(_source[i])) {
                comment = true;
            }
        }

        if (!newLine) {
            return false;
        }

        if (comment || !_plan.TryGap(next.SpanStart, out var spec)) {
            return true;
        }

        return spec.Rule switch {
            GapRule.Flat => false,
            GapRule.Mandatory => true,
            _ => GroupBreaks(spec.Group)
        };
    }

    /// <summary>Whether a group the plan created will hold its break points broken.</summary>
    /// <remarks>
    ///     ⚠ <see cref="GroupMode.Preserve" /> is the case that matters and the one that cannot be read
    ///     off the mode alone. A preserved group keeps the author's break — unless it is one of the
    ///     five the export lets join when it fits, and <c>place_simple_initializer_on_single_line</c>
    ///     is exactly that: <c>new[] { 1, 2, 3 }</c> written over five lines comes back as one, so a
    ///     rule that read `Preserve` as "broken" called the statement multi-line and wrote a blank the
    ///     oracle does not.
    ///     <para>
    ///         ⚠ <see cref="GroupMode.Auto" /> and <see cref="GroupMode.Owner" /> are answered "flat" and
    ///         left to the width test in <see cref="SpansLines" />, which asks the same question crudely.
    ///         An unknown id is answered "broken", because a group the plan did not describe is one this
    ///         code does not understand.
    ///     </para>
    /// </remarks>
    bool GroupBreaks(int group) {
        if (_groupPlans is null) {
            _groupPlans = new Dictionary<int, GroupPlan>();
            foreach (var plan in _plan.Groups) {
                _groupPlans[plan.Id] = plan;
            }
        }

        if (!_groupPlans.TryGetValue(group, out var found)) {
            return true;
        }

        return found.Mode switch {
            GroupMode.Flat => false,
            GroupMode.Break => true,
            GroupMode.Auto or GroupMode.Owner => false,
            _ => found.Facts.SourceBroken && !found.Facts.JoinsIfFits
        };
    }

    /// <summary>
    ///     Whether this piece is the last line of the comment block the file opens with.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three boundaries, each measured against the oracle at
    ///     <c>blank_lines_after_start_comment = 0</c> and <c>= 2</c> rather than assumed:
    ///     <list type="bullet">
    ///         <item>
    ///             A <c>///</c> or <c>/** */</c> run at position 0 is <b>not</b> a start comment. It is the
    ///             documentation of the type below it, and the oracle returns the file unchanged at both
    ///             values.
    ///         </item>
    ///         <item>
    ///             A comment that follows a directive is not a start comment either —
    ///             <c>#nullable enable</c> on line 1 and a <c>//</c> on line 2 is unchanged at both values.
    ///             The file has already started.
    ///         </item>
    ///         <item>
    ///             ⚠ Two <c>//</c> blocks separated by a blank line are <b>one</b> start comment, and the
    ///             gap the requirement lands in is the one under the <em>second</em>. So this is a property
    ///             of the run and not of the first piece.
    ///         </item>
    ///     </list>
    /// </remarks>
    bool IsStartComment(Piece previous) {
        if (previous.Kind is not (PieceKind.LineComment or PieceKind.BlockComment)) {
            return false;
        }

        for (var i = 0; i < _pieces.Length; i++) {
            if (_pieces[i].Kind is not (PieceKind.LineComment or PieceKind.BlockComment)) {
                return false;
            }

            if (_pieces[i].Span.End == previous.Span.End) {
                // The last piece of the run is the one whose successor is not a plain comment.
                return i + 1 >= _pieces.Length
                    || _pieces[i + 1].Kind is not (PieceKind.LineComment or PieceKind.BlockComment);
            }
        }

        return false;
    }

    /// <summary>
    ///     The requirement a switch section states about the gaps on either side of it.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Around", and both neighbours are asked. Measured on a switch whose first and fourth
    ///     sections are braced: at <c>blank_lines_around_block_case_section = 1</c> the oracle writes a
    ///     blank after the first, before the fourth and after the fourth, and nothing between the two
    ///     unbraced sections in between — which is "either side of the gap qualifies", not "the section
    ///     below does".
    ///     <para>
    ///         ⚠ A block section is one whose whole body is a single braced block, not one that merely
    ///         contains one. <c>case 3: var y = 3; y++; return y;</c> has three statements and is not a
    ///         block section, and the oracle leaves its gaps to the multiline key.
    ///     </para>
    /// </remarks>
    int SectionRequirement(SwitchSectionSyntax section) {
        var required = 0;
        if (section.Statements is [BlockSyntax]) {
            required = Math.Max(required, _options.BlankLinesAroundBlockCaseSection);
        }

        if (SpansLines(section)) {
            required = Math.Max(required, _options.BlankLinesAroundMultilineCaseSection);
        }

        return required;
    }

    /// <summary>
    ///     The statement whose last token this is, when it is an element of a statement list.
    /// </summary>
    /// <remarks>
    ///     ⚠ The list membership is the same guard <see cref="EndsAStatementInAList" /> carries and for
    ///     the same reason: a method body is a <see cref="BlockSyntax" />, which is a
    ///     <see cref="StatementSyntax" />, so without it every member's closing brace would answer
    ///     <c>blank_lines_after_multiline_statements</c> and take the gap the member rules own.
    ///     <para>
    ///         ⚠ Outward rather than innermost. An <c>else</c>'s last statement ends where the whole
    ///         <c>if</c> does, and the oracle spends the requirement on the <c>if</c>: at
    ///         <c>blank_lines_after_multiline_statements = 1</c> it writes one blank under
    ///         <c>if (a) return 1; else p++;</c>, which is the multi-line <c>if</c>'s and not the
    ///         single-line <c>p++;</c>'s.
    ///     </para>
    /// </remarks>
    static StatementSyntax? StatementEndingAt(SyntaxToken token) {
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.Span.End != token.Span.End) {
                return null;
            }

            if (node is StatementSyntax statement && IsStatementListElement(statement)) {
                return statement;
            }
        }

        return null;
    }

    /// <summary>
    ///     The statement that begins at this token, when the gap above it is the statement's own.
    /// </summary>
    /// <remarks>
    ///     ⚠ A statement an enclosing statement owns as its <em>embedded</em> statement is not a
    ///     subject, and this is the one boundary that cannot be guessed. Measured: at
    ///     <c>blank_lines_before_control_transfer_statements = 1</c> the oracle puts the blank above
    ///     <c>if (a &gt; 0)</c> and none above the <c>return 1;</c> on the line under it — the
    ///     requirement is spent once, on the statement the block's list holds. Reading it off the
    ///     <c>return</c> instead writes two blanks where the oracle writes one.
    ///     <para>
    ///         ⚠ A label is not such an owner, which is why <see cref="IsStatementListElement" /> admits
    ///         <see cref="LabeledStatementSyntax" />. ReSharper's tree keeps a label and the statement under
    ///         it as siblings where Roslyn nests them, and the oracle writes the blank between
    ///         <c>End:</c> and <c>return 2;</c> rather than above <c>End:</c>. Taking the outermost
    ///         statement that <em>starts at this token</em> reproduces that: the labelled statement starts
    ///         at <c>End</c>, so the subject at <c>return</c> is the return.
    ///     </para>
    /// </remarks>
    static StatementSyntax? StatementStartingAt(SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None)) {
            return null;
        }

        StatementSyntax? found = null;
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.Span.Start != token.Span.Start) {
                break;
            }

            if (node is StatementSyntax statement) {
                found = statement;
            }
        }

        if (found is null || !IsStatementListElement(found)) {
            return null;
        }

        // ⚠ The first statement of a switch section is not a subject. That gap belongs to the label
        // above it — `blank_lines_after_case` owns it — and the oracle spends nothing else there:
        // at `blank_lines_before_control_transfer_statements = 1` it writes no blank between
        // `case 2:` and the `return 2;` under it, while writing one before the third statement of a
        // section that has three. Without this the same key writes a blank under every `case` label
        // whose section starts with a `return` or a `break`, which is most of them.
        return found.Parent is SwitchSectionSyntax owner && owner.Statements.FirstOrDefault() == found
            ? null
            : found;
    }

    static bool IsStatementListElement(StatementSyntax statement) =>
        statement.Parent is BlockSyntax
            or SwitchSectionSyntax
            or GlobalStatementSyntax
            or LabeledStatementSyntax;

    /// <summary>
    ///     Whether a statement transfers control, counting the one it embeds.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>if (a) return 1;</c> counts and <c>if (a) { return 1; }</c> does not, which is measured
    ///     and not a distinction anyone would invent: the braced form puts the <c>return</c> inside a
    ///     block of its own, and the oracle writes no blank above such an <c>if</c> at
    ///     <c>blank_lines_before_control_transfer_statements = 1</c> while writing one above the
    ///     unbraced form. <see cref="BlockSyntax" /> therefore stops the recursion rather than
    ///     descending into it.
    /// </remarks>
    static bool IsControlTransfer(StatementSyntax statement) =>
        statement switch {
            ReturnStatementSyntax
                or BreakStatementSyntax
                or ContinueStatementSyntax
                or GotoStatementSyntax
                or ThrowStatementSyntax
                or YieldStatementSyntax => true,
            BlockSyntax => false,
            IfStatementSyntax owner => IsControlTransfer(owner.Statement)
                || owner.Else is { } clause
                && IsControlTransfer(clause.Statement),
            WhileStatementSyntax owner => IsControlTransfer(owner.Statement),
            ForStatementSyntax owner => IsControlTransfer(owner.Statement),
            CommonForEachStatementSyntax owner => IsControlTransfer(owner.Statement),
            DoStatementSyntax owner => IsControlTransfer(owner.Statement),
            UsingStatementSyntax owner => IsControlTransfer(owner.Statement),
            LockStatementSyntax owner => IsControlTransfer(owner.Statement),
            FixedStatementSyntax owner => IsControlTransfer(owner.Statement),
            _ => false
        };

    /// <summary>
    ///     Whether a statement owns a braced body — ReSharper's "statement with child blocks".
    /// </summary>
    /// <remarks>
    ///     ⚠ Three measured edges. A bare <c>{ … }</c> block is <b>not</b> one, which is the reading of
    ///     the option's name nobody would pick: it is a block, not a statement <em>with</em> a block. A
    ///     <c>switch</c> <b>is</b> one although its braces hold sections rather than a
    ///     <see cref="BlockSyntax" />. And a local function is not one — with
    ///     <c>blank_lines_around_local_method</c> turned off so it could be seen, the oracle writes no
    ///     blank above <c>void Local() { … }</c> at <c>blank_lines_before_block_statements = 1</c>,
    ///     while it does write one above every <c>if</c>, <c>lock</c>, <c>do</c>, <c>using</c>,
    ///     <c>checked</c>, <c>unsafe</c>, <c>fixed</c> and <c>try</c>.
    ///     <para>
    ///         ⚠ Direct children only. <c>Action f = () =&gt; { … };</c> holds a block two levels down and
    ///         is not a block statement.
    ///     </para>
    /// </remarks>
    static bool HasChildBlock(StatementSyntax statement) {
        if (statement is BlockSyntax or LocalFunctionStatementSyntax) {
            return false;
        }

        if (statement is SwitchStatementSyntax) {
            return true;
        }

        foreach (var child in statement.ChildNodes()) {
            if (child is BlockSyntax) {
                return true;
            }

            // `else`, `catch` and `finally` hold their block one node down, and an `if` whose only
            // braces are the else's is still a statement with a child block.
            if (child is ElseClauseSyntax or CatchClauseSyntax or FinallyClauseSyntax
                && child.ChildNodes().Any(static grandchild => grandchild is BlockSyntax)) {
                return true;
            }
        }

        return false;
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
    ///     The columns the member's own text will occupy once the formatter has respaced it.
    /// </summary>
    /// <remarks>
    ///     ⚠ A function of the token stream, never of the gaps between the tokens in the source. That
    ///     is the whole point: the caller is deciding a blank line, and a decision that reads whitespace
    ///     the formatter is about to rewrite is not absorbed by
    ///     <c>
    /// format(mutate_whitespace(x)) ≡
    ///  format(x)
    ///     </c>.
    ///     <para>
    ///         The one place the source is still consulted is <see cref="SpaceKind.Preserve" />, and there it
    ///         is correct rather than tolerated: an ungoverned gap is one <c>extra_spaces = remove_all</c>
    ///         collapses to whatever the author had — one space or none — so the output really does carry
    ///         that bit, and reading it is reading the output. Widening such a gap does not change the
    ///         answer, because a run and a single space collapse alike.
    ///     </para>
    ///     <para>
    ///         ⚠ Only ever called for a member <see cref="IsSingleLine" /> has already found on one source
    ///         line, so no token here spans lines and <see cref="TextWidth.Measure" />'s newline reset cannot
    ///         be reached.
    ///     </para>
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

        return width + TrailingCommentWidth(previous);
    }

    /// <summary>
    ///     The comment that will share the member's last line, after its last token.
    /// </summary>
    /// <remarks>
    ///     ⚠ <see cref="GapWidth" /> measures every comment <em>between</em> two of a member's tokens
    ///     and there is no gap after the last one, so without this the width the caller compares to the
    ///     margin is not the width of the line the fitter will lay out. That is a non-idempotency
    ///     rather than a rounding error, because the two disagree in exactly one direction:
    ///     <c>internal … M97(out decimal p98) => [(static x =&gt; 3_000_000L), items[1..]]; // fuzz</c>
    ///     measures 116 columns without the comment and 124 with it, so
    ///     <see cref="IsSingleLine" /> called it single-line and declined the blank line that
    ///     <c>blank_lines_around_single_line_invocable = 0</c> governs — and then the fitter, which
    ///     does count the comment, chopped the member onto three lines. The second pass reads a
    ///     three-line member, asks <c>blank_lines_around_invocable = 1</c> instead, and inserts the
    ///     blank line the first pass had refused. SK-FUZZ-0010, and SK-FUZZ-0007's mistake one step
    ///     further out: every width this decision reads has to be a width of the <em>output</em>.
    ///     <para>
    ///         ⚠ Stops at the first end-of-line. Roslyn hangs a token's trailing trivia through the newline
    ///         that ends its line, so anything past that terminator is on a line this member does not own.
    ///     </para>
    /// </remarks>
    int TrailingCommentWidth(SyntaxToken last) {
        var width = 0;
        var comments = false;
        foreach (var trivia in last.TrailingTrivia) {
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia)) {
                break;
            }

            Measure(trivia, ref width, ref comments);
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
