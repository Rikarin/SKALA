using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>
/// The three-system blank-line resolution.
/// </summary>
/// <remarks>
/// docs/plan/04 § "Blank lines": <b>removals ∘ requirements ∘ caps</b>, evaluated on the gap
/// between two members and attributed to the member below — so that <c>stick_comment</c> moves the
/// right blank lines with the comment.
/// <para>
/// ⚠ This is the highest bug density in the whole formatter, because the three systems interact and
/// hand-reasoning about them is unreliable. It is verified against the oracle on
/// <c>constructs/blank-lines/</c>, not argued about.
/// </para>
/// </remarks>
public sealed partial class CSharpDocumentBuilder {
    int ResolveBlankLines(Piece previous, int nextPieceIndex, SyntaxToken nextToken, int sourceBlanks) {
        var declaration = nextToken.IsKind(SyntaxKind.None) || InDeclarationContext(nextToken);

        // 1. Caps. The author's runs are truncated, never extended.
        var cap = Math.Max(0, declaration ? _options.KeepBlankLinesInDeclarations : _options.KeepBlankLinesInCode);
        var blanks = Math.Min(sourceBlanks, cap);

        // 2. Requirements. A minimum inserted where absent.
        blanks = Math.Max(blanks, RequiredBlankLines(previous, nextPieceIndex, nextToken));

        // 3. Removals, which win over (2).
        if (RemovesNearBrace(previous, nextToken, declaration)) {
            blanks = 0;
        }

        return blanks;
    }

    bool RemovesNearBrace(Piece previous, SyntaxToken nextToken, bool declaration) {
        var enabled = declaration ? _options.RemoveBlankLinesNearBracesInDeclarations : _options.RemoveBlankLinesNearBracesInCode;
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
        if (nextPieceIndex >= 0 && nextPieceIndex < _pieces.Length && _pieces[nextPieceIndex].Kind == PieceKind.RegionDirective
            || previous.Kind == PieceKind.RegionDirective) {
            required = Math.Max(required, RegionRequirement(previous, nextPieceIndex));
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
            // separated from the next one.
            if (previousToken.IsKind(SyntaxKind.CloseBraceToken) && previousToken.Parent is BlockSyntax { Parent: StatementSyntax }) {
                required = Math.Max(required, _options.BlankLinesAfterBlockStatements);
            }

            if (previousToken.Parent is SwitchLabelSyntax) {
                required = Math.Max(required, _options.BlankLinesAfterCase);
            }
        }

        if (!nextToken.IsKind(SyntaxKind.None) && nextToken.Parent is SwitchLabelSyntax
            && nextToken.Parent.Parent is SwitchSectionSyntax section
            && section.Parent is SwitchStatementSyntax owner && owner.Sections.IndexOf(section) > 0) {
            required = Math.Max(required, _options.BlankLinesBeforeCase);
        }

        if (nextPieceIndex >= 0 && nextPieceIndex < _pieces.Length
            && _pieces[nextPieceIndex].Kind == PieceKind.LineComment && _pieces[nextPieceIndex].StartsLine) {
            required = Math.Max(required, _options.BlankLinesBeforeSingleLineComment);
        }

        var above = previous.Kind == PieceKind.Token ? MemberEndingAt(_tokens[previous.TokenIndex]) : null;
        var below = MemberStartingAt(nextPieceIndex, nextToken);

        if (above is null && below is null) {
            return required;
        }

        // ⚠ blank_lines_after_using_list applies to the boundary, not to either member's own rule.
        if (above is UsingDirectiveSyntax or ExternAliasDirectiveSyntax && below is not (UsingDirectiveSyntax or ExternAliasDirectiveSyntax)) {
            required = Math.Max(required, _options.BlankLinesAfterUsingList);
        }

        if (above is FileScopedNamespaceDeclarationSyntax || previous.Kind == PieceKind.Token && EndsFileScopedNamespaceDirective(_tokens[previous.TokenIndex])) {
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

    int RegionRequirement(Piece previous, int nextPieceIndex) {
        var nextIsRegion = nextPieceIndex >= 0 && nextPieceIndex < _pieces.Length && _pieces[nextPieceIndex].Kind == PieceKind.RegionDirective;
        var previousIsRegion = previous.Kind == PieceKind.RegionDirective;

        // Right inside the braces the removal rule wins anyway; elsewhere a region is separated
        // from the code around it.
        if (nextIsRegion && previousIsRegion) {
            return _options.BlankLinesInsideRegion;
        }

        // The gap just inside a region — after `#region`, before `#endregion` — is the region's
        // inside, not its outside.
        var opensBefore = previousIsRegion && previous.Text.StartsWith("#region", StringComparison.Ordinal);
        var closesAfter = nextIsRegion
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
            MethodDeclarationSyntax or ConstructorDeclarationSyntax or DestructorDeclarationSyntax
                or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax =>
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
        property.AccessorList is { } accessors && accessors.Accessors.All(static a => a.Body is null && a.ExpressionBody is null);

    /// <summary>
    /// One line including whatever <c>stick_comment</c> attached to it: a method with a doc comment
    /// is not single-line even when its body is, which is what docs/plan/05 § "Blank lines" means
    /// by "M() has a doc comment and is not single-line".
    /// </summary>
    bool IsSingleLine(SyntaxNode member) {
        var start = _options.StickComment ? StickyStart(member) : member.Span.Start;
        var lines = _text.Lines;
        return lines.GetLineFromPosition(start).LineNumber == lines.GetLineFromPosition(member.Span.End).LineNumber;
    }

    /// <summary>
    /// Where a member starts once the comment block directly above it is counted as part of it.
    /// </summary>
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

                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    start = trivia.FullSpan.Start;
                    continue;

                default:
                    return start;
            }
        }

        return start;
    }

    /// <summary>The outermost declaration whose last token is <paramref name="token"/>.</summary>
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
    /// The outermost declaration that starts at the next piece — the comment above it included, so
    /// the gap is attributed to the member below rather than to the comment.
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
        node is MemberDeclarationSyntax or AccessorDeclarationSyntax or UsingDirectiveSyntax
            or ExternAliasDirectiveSyntax or LocalFunctionStatementSyntax;

    /// <summary>
    /// Declarations or code? The caps differ (<c>keep_blank_lines_in_declarations</c> against
    /// <c>keep_blank_lines_in_code</c>) and so do the removal keys.
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
}
