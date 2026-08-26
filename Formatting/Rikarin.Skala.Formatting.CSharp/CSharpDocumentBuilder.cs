using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>The document, plus what building it had to say.</summary>
public sealed record BuiltDocument(Document Document, IReadOnlyList<SkalaDiagnostic> Diagnostics);

/// <summary>
/// Turns a parsed C# file into the language-agnostic document IR.
/// </summary>
/// <remarks>
/// The walk is structural — one pass over the syntax tree — with an ordered piece stream
/// (<see cref="SourcePieces"/>) threaded through it, so that a comment between two tokens is
/// emitted at the nesting the tokens around it establish rather than at whichever token Roslyn
/// happened to attach it to. That is what docs/plan/04 means by "which token owns this comment must
/// be a decision Skala makes, not one it inherits".
/// <para>
/// ⚠ Milestone 1 never moves a line to fit a width. Every gap that held a line break still holds
/// one and every gap that did not, does not — except where a brace rule joins one, which is a
/// decision rather than a fit.
/// </para>
/// </remarks>
public sealed partial class CSharpDocumentBuilder {
    readonly SourceText _text;
    readonly string _source;
    readonly Piece[] _pieces;
    readonly SyntaxToken[] _tokens;
    readonly PhaseOneOptions _options;
    readonly DocumentBuilder _doc = new();
    readonly List<SkalaDiagnostic> _diagnostics = [];
    readonly List<int> _blockStack = [];

    /// <summary>One per open statement, member, accessor or call chain, and per block scope.</summary>
    readonly List<Frame> _frames = [];

    readonly HashSet<int> _verbatimMembers = [];
    readonly string _path;
    BreakPlan _plan = null!;

    int _cursor;
    int _lastPiece = -1;

    /// <summary>Where <see cref="EmitLeadingGap"/> already wrote a gap, so it is not written twice.</summary>
    int _gapEmittedAt = -1;
    int _verbatimUntil = -1;
    int _continuousDepth;

    CSharpDocumentBuilder(string path, SourceText text, SyntaxNode root, in PhaseOneOptions options) {
        _path = path;
        _text = text;
        _source = text.ToString();
        _options = options;
        (_pieces, _tokens) = SourcePieces.Split(root, text);
    }

    public static BuiltDocument Build(string path, SourceText text, SyntaxNode root, in PhaseOneOptions options) {
        var builder = new CSharpDocumentBuilder(path, text, root, options);
        builder.Run(root);
        return new BuiltDocument(builder._doc.Build(), builder._diagnostics);
    }

    void Run(SyntaxNode root) {
        PreprocessorGuard.MarkUnbalancedMembers(root, _text, _verbatimMembers, _diagnostics, _path);

        // ⚠ The break plan is built before the walk, not during it: a gap can belong to two
        // constructs at once and only a pass that sees both can decide which one owns it
        // (see BreakPlan's remarks). Ids are handed out here so that the plan's numbering and the
        // document's agree.
        _plan = BreakPlan.Build(root, _source, _options);
        for (var i = 0; i < _plan.GroupCount; i++) {
            _doc.NextGroupId();
        }

        foreach (var planned in _plan.Groups) {
            _doc.DescribeGroup(planned.Id, planned.Facts);
        }

        var group = _doc.NextGroupId();
        _doc.OpenGroup(GroupMode.Flat, group);
        Visit(root);
        EmitUpTo(int.MaxValue);
        _doc.Close();
    }

    // ── The structural walk ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Visits a node, opening a continuation frame for the constructs a continuation break can be
    /// attributed to.
    /// </summary>
    /// <remarks>
    /// ⚠ The frame is what turns the one continuous indent level into a <em>scope</em> rather than a
    /// per-line adjustment, and the difference is not cosmetic:
    /// <code>
    /// void N() =>
    ///     Q(          ← the arrow's continuation level, +1
    ///         a,      ← and the parenthesis's, +2 — which only composes if the first is a scope
    ///     );
    /// </code>
    /// </remarks>
    void Visit(SyntaxNode node) {
        if (!_plan.TryGroup(node, out var planned)) {
            VisitInner(node);
            return;
        }

        // ⚠ The gap *before* the construct is emitted first, outside the group. It belongs to
        // whatever encloses the construct, and a group that swallows it is measured wrong twice
        // over: the break makes its flat width infinite, so it can never be flat, and the column it
        // is entered at is the one before that break rather than the one after it. The symptom is a
        // statement-level group that never joins and never fits — `if (flag)\n First();` measured as
        // starting at column 23 on the previous line.
        EmitLeadingGap(node);
        _doc.OpenGroup(planned.Mode, planned.Id);

        // ⚠ The continuation scope a group's own break points need is opened here, inside the group
        // and closed inside it, rather than lazily at the break. Lazily is what milestone 1 did and
        // it is fine while the document stack holds nothing but indent scopes; a group on the same
        // stack closes before the statement that owns the frame does, and pops the indent instead of
        // itself.
        var indented = planned.SpendsIndent && CanSpendAContinuationLevel();
        if (indented) {
            OpenIndent(IndentKind.Continuous);
        }

        VisitInner(node);
        EmitUpTo(node.Span.End);
        if (indented) {
            CloseIndent(IndentKind.Continuous);
        }

        _doc.Close();
    }

    /// <summary>
    /// Whether a continuation level is this group's to spend.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>continuous_line_indent = single</c> as docs/plan/04 § "Indentation" corrects it: a
    /// delimited group already open supplies the level, and an undelimited continuation that is
    /// already inside another one adds nothing. <c>M(\n a\n + b)</c> takes the parenthesis's level
    /// and not a second one.
    /// </remarks>
    bool CanSpendAContinuationLevel() {
        if (_continuousDepth != 0) {
            return false;
        }

        for (var i = _frames.Count - 1; i >= 0; i--) {
            if (!_frames[i].Started) {
                continue;
            }

            return !_frames[i].Activated;
        }

        return true;
    }

    /// <summary>Emits everything before <paramref name="node"/>'s first piece, its gap included.</summary>
    void EmitLeadingGap(SyntaxNode node) {
        EmitUpTo(node.SpanStart);
        if (_cursor >= _pieces.Length || _lastPiece < 0) {
            return;
        }

        var piece = _pieces[_cursor];

        // ⚠ Nested groups can start at the same token — a binary chain and its leftmost operand,
        // an invocation and the member access inside it — and the gap before that token is one gap.
        // Emitting it twice writes two breaks and, worse, opens a continuation scope that is closed
        // once.
        if (piece.Span.Start < _verbatimUntil || piece.Span.Start == _gapEmittedAt) {
            return;
        }

        EmitGap(_cursor, piece.Kind, piece.Span.Start, piece.Kind == PieceKind.Token ? _tokens[piece.TokenIndex] : default);
        _gapEmittedAt = piece.Span.Start;
    }

    void VisitInner(SyntaxNode node) {
        // ⚠ A chained method call takes a continuation level of its own; a binary chain in the same
        // position does not. Verified against the oracle, because the two look identical on paper:
        //   Q(                         Q(
        //       a                          new[] { … }
        //       + b,          vs               .Select(…)   ← one level deeper
        //       c);                            .ToArray(),
        //                              c);
        // The level is spent lazily, at the first break before a `.`, so a chain that does not
        // break costs nothing and an argument list inside one is not pushed twice.
        // ⚠ A binary PATTERN chain takes a level of its own; a binary EXPRESSION chain does not.
        // `wrap_chained_binary_patterns` and `wrap_chained_binary_expressions` are separate keys and
        // ReSharper treats them differently:
        //   x is A            a
        //       or B    vs    + b     ← one level, not two
        if (IsChainRoot(node) || IsPatternChainRoot(node)) {
            _frames.Add(new Frame(IsPatternChainRoot(node) ? FrameKind.Pattern : FrameKind.Chain, false));
            Dispatch(node);
            if (_frames[^1].Activated) {
                CloseIndent(IndentKind.Continuous);
            }

            _frames.RemoveAt(_frames.Count - 1);
            return;
        }

        if (!OwnsAContinuationFrame(node)) {
            Dispatch(node);
            return;
        }

        // ⚠ A lambda body is its own continuation context. Without the reset, a chain broken inside
        // a lambda that is itself inside an argument list sees a delimited scope already open and
        // declines the level ReSharper gives it.
        //
        // The reset is deferred to the frame's first piece, not applied here: the break that lands
        // just before the lambda is still the enclosing member's to pay for, and zeroing the depth
        // early lets the member's own level be spent inside the lambda's frame instead.
        var saved = _continuousDepth;
        _frames.Add(new Frame(FrameKind.Unit, false, ResetsDepth: node is AnonymousFunctionExpressionSyntax));
        Dispatch(node);
        if (_frames[^1].Activated) {
            CloseIndent(IndentKind.Continuous);
        }

        _frames.RemoveAt(_frames.Count - 1);
        _continuousDepth = saved;
    }

    /// <summary>
    /// A break is attributed to the innermost statement, member or accessor, because those are the
    /// units whose continuation lines the option is about. A block resets the count, so the frame a
    /// break lands on is always inside the nearest brace.
    /// </summary>
    /// <summary>
    /// The outermost link of a <c>a.B().C()</c> chain — the one whose level the whole chain hangs
    /// from.
    /// </summary>
    static bool IsChainRoot(SyntaxNode node) =>
        // ⚠ The root is the outermost link, and for `a.B().C()` that is the invocation, not the
        // member access inside it. Testing only for a member access finds the wrong node and the
        // chain never spends its level.
        node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax or MemberBindingExpressionSyntax }
            or MemberAccessExpressionSyntax or ConditionalAccessExpressionSyntax
        && node.Parent is not (InvocationExpressionSyntax or MemberAccessExpressionSyntax
            or ElementAccessExpressionSyntax or ConditionalAccessExpressionSyntax
            or MemberBindingExpressionSyntax or PostfixUnaryExpressionSyntax);

    static bool IsPatternChainRoot(SyntaxNode node) =>
        node is BinaryPatternSyntax && node.Parent is not BinaryPatternSyntax;

    /// <summary>
    /// The constructs a continuation level is attributed to.
    /// </summary>
    /// <remarks>
    /// ⚠ List elements are on this list, and they have to be. A level spent inside one arm of a
    /// switch expression must not still be open when the next arm starts — the leak shifts every
    /// following arm right by four and is invisible until a long file has one wrapped arm in the
    /// middle of it.
    /// </remarks>
    static bool OwnsAContinuationFrame(SyntaxNode node) =>
        node is StatementSyntax or MemberDeclarationSyntax or AccessorDeclarationSyntax
            or AnonymousFunctionExpressionSyntax or SwitchExpressionArmSyntax or ArgumentSyntax
            or AttributeArgumentSyntax or ParameterSyntax or AnonymousObjectMemberDeclaratorSyntax
            or VariableDeclaratorSyntax or SubpatternSyntax or CollectionElementSyntax
            or SwitchLabelSyntax or BaseTypeSyntax or TypeParameterConstraintClauseSyntax;

    void Dispatch(SyntaxNode node) {
        if (_verbatimMembers.Contains(node.SpanStart) && node is MemberDeclarationSyntax) {
            EmitVerbatim(node);
            return;
        }

        switch (NodeLayouts.Classify(node.Kind())) {
            case NodeLayout.Unknown:
            case NodeLayout.Verbatim:
                // ⚠ R5: a kind this Skala was never told about is emitted from its original span
                // rather than guessed at. The same path serves interpolated strings, where a moved
                // space changes the value.
                EmitVerbatim(node);
                return;

            case NodeLayout.BracedBlock:
            case NodeLayout.BracedInitializer:
                VisitBraced(node);
                return;

            case NodeLayout.Parens:
            case NodeLayout.Brackets:
            case NodeLayout.Angles:
                VisitDelimited(node, NodeLayouts.Classify(node.Kind()));
                return;

            case NodeLayout.Embedded:
                VisitEmbedded(node);
                return;

            case NodeLayout.SwitchBody:
                VisitSwitch((SwitchStatementSyntax)node);
                return;

            case NodeLayout.SwitchSection:
                VisitSwitchSection((SwitchSectionSyntax)node);
                return;

            case NodeLayout.Continuation when node is ConditionalExpressionSyntax ternary:
                // ⚠ A ternary's arms take a level of their own, on top of whatever continuation the
                // expression already sits in — `outdent_ternary_ops = false`. A binary chain does
                // not, which is why the two are not the same case.
                Visit(ternary.Condition);
                OpenIndent(IndentKind.Continuous);
                EmitToken(ternary.QuestionToken);
                Visit(ternary.WhenTrue);
                EmitToken(ternary.ColonToken);
                Visit(ternary.WhenFalse);
                EmitUpTo(ternary.Span.End);
                CloseIndent(IndentKind.Continuous);
                return;

            case NodeLayout.Continuation when node is TypeParameterConstraintClauseSyntax:
                // resharper_indent_type_constraints: a `where` clause on its own line is a
                // continuation of the declaration, and the option says whether it takes a level.
                if (!_options.IndentTypeConstraints) {
                    VisitChildren(node);
                    return;
                }

                OpenIndent(IndentKind.Continuous);
                VisitChildren(node);
                CloseIndent(IndentKind.Continuous);
                return;

            default:
                VisitChildren(node);
                return;
        }
    }

    void VisitChildren(SyntaxNode node) {
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                EmitToken(child.AsToken());
            } else if (child.AsNode() is { } inner) {
                VisitChild(node, inner);
            }
        }
    }

    /// <summary>
    /// Visits a child, spending the owner's continuation level first when the child is the owner's
    /// own body.
    /// </summary>
    /// <remarks>
    /// ⚠ A body indents from its declaration's level, not from whatever line the brace ended up on:
    /// <code>
    /// protected C(int a) :
    ///     base(a) {        ← the initializer's continuation level
    ///     Body();          ← but the body is one from the CONSTRUCTOR, not two
    /// }
    /// </code>
    /// The test is that the block is the frame owner's own child; a lambda's block nested inside an
    /// expression keeps the continuation, because there the level is real.
    /// </remarks>
    void VisitChild(SyntaxNode owner, SyntaxNode child) {
        if (child is BlockSyntax or AccessorListSyntax && OwnsAContinuationFrame(owner) && _frames.Count > 0 && _frames[^1].Activated) {
            CloseIndent(IndentKind.Continuous);
            _frames[^1] = _frames[^1] with { Activated = false };
        }

        Visit(child);
    }

    /// <summary>A <c>{ }</c> body: everything between the braces takes one block indent.</summary>
    void VisitBraced(SyntaxNode node) {
        var (open, close) = BraceTokens(node);
        var opened = false;

        // csharp_indent_braces: the braces themselves take the inner level rather than the outer.
        var indentBraces = _options.IndentBraces;

        // resharper_indent_inside_namespace = false flattens a block namespace's members.
        var suppress = node is NamespaceDeclarationSyntax && !_options.IndentInsideNamespace;

        // use_continuous_indent_inside_initializer_braces = false leaves an initializer's contents
        // at the level of the construct that owns them.
        if (node is InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax && !_options.UseContinuousIndentInsideInitializerBraces) {
            suppress = true;
        }

        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (opened && !close.IsKind(SyntaxKind.None) && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    if (!indentBraces) {
                        CloseIndent(IndentKind.Block, alignsCloser: true);
                    }

                    EmitToken(token);
                    if (indentBraces) {
                        CloseIndent(IndentKind.Block);
                    }

                    opened = false;
                    continue;
                }

                if (!opened && !open.IsKind(SyntaxKind.None) && token.SpanStart == open.SpanStart && !suppress) {
                    // ⚠ Same rule as VisitChild's: a body indents from its declaration's level.
                    // `class C\n    : B {` puts the base list on a continuation line, and the members
                    // still take one level from the class rather than two.
                    if (OwnsAContinuationFrame(node) && _frames.Count > 0 && _frames[^1].Activated) {
                        CloseIndent(IndentKind.Continuous);
                        _frames[^1] = _frames[^1] with { Activated = false };
                    }

                    if (indentBraces) {
                        OpenIndent(IndentKind.Block);
                        EmitToken(token);
                    } else {
                        EmitToken(token);
                        OpenIndent(IndentKind.Block);
                    }

                    opened = true;
                    continue;
                }

                EmitToken(token);
            } else if (child.AsNode() is { } inner) {
                VisitChild(node, inner);
            }
        }

        if (opened) {
            EmitUpTo(close.IsKind(SyntaxKind.None) ? int.MaxValue : close.SpanStart);
            CloseIndent(IndentKind.Block);
        }
    }

    /// <summary>A <c>( )</c>, <c>[ ]</c> or <c>&lt; &gt;</c> group: one continuous indent inside.</summary>
    void VisitDelimited(SyntaxNode node, NodeLayout layout) {
        var (open, close) = DelimiterTokens(node, layout);
        if (open.IsKind(SyntaxKind.None) || close.IsKind(SyntaxKind.None)) {
            VisitChildren(node);
            return;
        }

        var opened = false;
        var suppress = layout == NodeLayout.Parens && !_options.UseContinuousIndentInsideParens;

        // ⚠ A collection expression's elements are elements, like an initializer's: a chain broken
        // inside one takes its own continuation level rather than living off the bracket's.
        var element = node is CollectionExpressionSyntax or ListPatternSyntax;
        var savedDepth = _continuousDepth;
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (opened && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    if (element) {
                        if (_frames[^1].Activated) {
                            _doc.Close();
                        }

                        _frames.RemoveAt(_frames.Count - 1);
                        _continuousDepth = savedDepth;
                    }

                    CloseIndent(IndentKind.Continuous, alignsCloser: true);
                    opened = false;
                }

                EmitToken(token);

                if (!opened && !suppress && token.SpanStart == open.SpanStart) {
                    OpenIndent(IndentKind.Continuous);
                    opened = true;
                    if (element) {
                        savedDepth = _continuousDepth;
                        _continuousDepth = 0;
                        _frames.Add(new Frame(FrameKind.Unit, false));
                    }
                }
            } else if (child.AsNode() is { } inner) {
                VisitChild(node, inner);
            }
        }

        if (opened) {
            EmitUpTo(close.SpanStart);
            if (element) {
                if (_frames[^1].Activated) {
                    _doc.Close();
                }

                _frames.RemoveAt(_frames.Count - 1);
                _continuousDepth = savedDepth;
            }

            CloseIndent(IndentKind.Continuous);
        }
    }

    /// <summary>
    /// A statement whose embedded statement indents when it is not a block, and whose condition
    /// parentheses are a continuation scope of their own.
    /// </summary>
    void VisitEmbedded(SyntaxNode node) {
        var embedded = EmbeddedStatement(node);
        var (open, close) = ConditionParentheses(node);
        var parenOpen = false;

        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                var token = child.AsToken();
                if (parenOpen && token.SpanStart == close.SpanStart) {
                    EmitUpTo(close.SpanStart);
                    CloseIndent(IndentKind.Continuous, alignsCloser: true);
                    parenOpen = false;
                }

                EmitToken(token);

                if (!parenOpen && !open.IsKind(SyntaxKind.None) && token.SpanStart == open.SpanStart) {
                    OpenIndent(IndentKind.Continuous);
                    parenOpen = true;
                }

                continue;
            }

            if (child.AsNode() is not { } inner) {
                continue;
            }

            if (embedded is not null && inner == embedded && NeedsEmbeddedIndent(node, embedded)) {
                OpenIndent(IndentKind.Block);
                Visit(inner);
                CloseIndent(IndentKind.Block);
                continue;
            }

            VisitChild(node, inner);
        }

        if (parenOpen) {
            EmitUpTo(close.SpanStart);
            CloseIndent(IndentKind.Continuous);
        }
    }

    void VisitSwitch(SwitchStatementSyntax node) {
        EmitToken(node.SwitchKeyword);
        if (!node.OpenParenToken.IsKind(SyntaxKind.None)) {
            EmitToken(node.OpenParenToken);
            OpenIndent(IndentKind.Continuous);
            Visit(node.Expression);
            EmitUpTo(node.CloseParenToken.SpanStart);
            CloseIndent(IndentKind.Continuous, alignsCloser: true);
            EmitToken(node.CloseParenToken);
        } else {
            Visit(node.Expression);
        }

        EmitToken(node.OpenBraceToken);

        // csharp_indent_switch_labels = true: the labels take one indent from the switch.
        var labelled = _options.IndentSwitchLabels;
        if (labelled) {
            OpenIndent(IndentKind.Block);
        }

        foreach (var section in node.Sections) {
            Visit(section);
        }

        EmitUpTo(node.CloseBraceToken.SpanStart);
        if (labelled) {
            CloseIndent(IndentKind.Block, alignsCloser: true);
        }

        EmitToken(node.CloseBraceToken);
    }

    void VisitSwitchSection(SwitchSectionSyntax node) {
        foreach (var label in node.Labels) {
            Visit(label);
        }

        if (node.Statements.Count == 0) {
            return;
        }

        // ⚠ A case whose whole body is a block already gets its level from the braces. Adding the
        // section's own level too puts `case X: {` two levels above its contents, which is the
        // shape ReSharper does not produce.
        var braced = node.Statements is [BlockSyntax];
        if (braced) {
            foreach (var statement in node.Statements) {
                Visit(statement);
            }

            EmitUpTo(node.Span.End);
            return;
        }

        // The statements of a case take one indent from the label.
        OpenIndent(IndentKind.Block);
        foreach (var statement in node.Statements) {
            // ⚠ resharper_indent_break_from_case = false puts the control transfer back at the
            // label's own level, which is a different shape and not a rounding error.
            if (!_options.IndentBreakFromCase && statement is BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax) {
                OpenIndent(IndentKind.Outdent);
                Visit(statement);
                CloseIndent(IndentKind.Outdent);
                continue;
            }

            Visit(statement);
        }

        EmitUpTo(node.Span.End);
        CloseIndent(IndentKind.Block);
    }

    // ── Indent scopes ────────────────────────────────────────────────────────────────────────

    void OpenIndent(IndentKind kind) {
        _doc.OpenIndent(kind);
        if (kind == IndentKind.Outdent) {
            return;
        }

        if (kind == IndentKind.Continuous) {
            _continuousDepth++;
            return;
        }

        _blockStack.Add(_continuousDepth);
        _continuousDepth = 0;

        // ⚠ A block is a frame boundary. A continuation level spent inside it must be closed inside
        // it too, or the document builder's Close pops the wrong container and the whole brace
        // structure of the file shifts by one.
        _frames.Add(new Frame(FrameKind.Unit, false));
    }

    /// <param name="alignsCloser">
    /// The next piece is this scope's own closing delimiter and takes its opener's line level.
    /// </param>
    void CloseIndent(IndentKind kind, bool alignsCloser = false) {
        if (kind == IndentKind.Outdent) {
            _doc.Close(alignsCloser);
            return;
        }

        if (kind == IndentKind.Continuous) {
            _continuousDepth--;
        } else {
            if (_frames[^1].Activated) {
                _doc.Close();
            }

            _frames.RemoveAt(_frames.Count - 1);
            _continuousDepth = _blockStack[^1];
            _blockStack.RemoveAt(_blockStack.Count - 1);
        }

        _doc.Close(alignsCloser);
    }

    // ── The piece stream ─────────────────────────────────────────────────────────────────────

    void EmitToken(SyntaxToken token) {
        if (token.IsKind(SyntaxKind.None) || token.IsMissing && token.Span.Length == 0) {
            return;
        }

        EmitUpTo(token.SpanStart);
        if (_cursor < _pieces.Length
            && _pieces[_cursor].Span.Start == token.SpanStart
            && _pieces[_cursor].Kind == PieceKind.Token) {
            EmitPiece(_cursor++);
        }
    }

    void EmitUpTo(int position) {
        while (_cursor < _pieces.Length && _pieces[_cursor].Span.Start < position) {
            EmitPiece(_cursor++);
        }
    }

    /// <summary>A whole node written from its original span: never reindented, never respaced.</summary>
    void EmitVerbatim(SyntaxNode node) {
        EmitUpTo(node.SpanStart);
        var span = node.Span;
        if (span.Start != _gapEmittedAt) {
            EmitGap(_cursor, PieceKind.Token, span.Start, node.GetFirstToken());
        }

        MarkFramesStarted();

        var source = new SourceSpan(span.Start, span.Length);
        _doc.Anchor(source, -1);
        // The node's first line takes the code's indentation; its interior lines are never
        // reindented, because the writer only indents at a line start and this text is one piece.
        _doc.Verbatim(_source[span.Start..span.End], source);

        while (_cursor < _pieces.Length && _pieces[_cursor].Span.Start < span.End) {
            _lastPiece = _cursor;
            _cursor++;
        }
    }

    void EmitPiece(int index) {
        var piece = _pieces[index];

        // Inside a `@formatter:off` span everything was written as one raw chunk already.
        if (piece.Span.Start < _verbatimUntil) {
            _lastPiece = index;
            return;
        }

        if (_options.FormatterTagsEnabled && piece.IsComment && ContainsTag(piece.Text, _options.FormatterOffTag)) {
            EmitFormatterOffSpan(index);
            return;
        }

        var token = piece.Kind == PieceKind.Token ? _tokens[piece.TokenIndex] : default;
        if (piece.Span.Start != _gapEmittedAt) {
            EmitGap(index, piece.Kind, piece.Span.Start, token);
        }

        MarkFramesStarted();

        var span = new SourceSpan(piece.Span.Start, piece.Span.Length);
        _doc.Anchor(span, piece.TokenIndex);

        switch (piece.Kind) {
            case PieceKind.Token:
                _doc.Text(piece.Text, span);
                break;

            case PieceKind.DisabledText:
            case PieceKind.Skipped:
                // ⚠ Never reindented. Silently doing something clever here is how formatters
                // destroy code (docs/plan/04 § "Trivia").
                _doc.Verbatim(piece.Text, span, VerbatimFlags.SelfIndented);
                break;

            case PieceKind.ConditionalDirective:
                _doc.Verbatim(piece.Text, span, DirectiveFlags(_options.IndentPreprocessorIf));
                break;

            case PieceKind.OtherDirective:
                _doc.Verbatim(piece.Text, span, DirectiveFlags(_options.IndentPreprocessorOther));
                break;

            case PieceKind.RegionDirective:
                _doc.Verbatim(piece.Text, span, DirectiveFlags(_options.IndentPreprocessorRegion));
                break;

            case PieceKind.BlockComment:
            case PieceKind.BlockDocComment:
                // A multi-line comment's continuation lines carry their own indentation; the first
                // line takes the code's.
                _doc.Verbatim(piece.Text, span, CommentFlags(piece));
                break;

            case PieceKind.DocCommentLine:
                _doc.Text(SpaceAfterMarker(piece.Text, "///", _options.SpaceAfterTripleSlash), span, CommentFlags(piece));
                break;

            case PieceKind.LineComment:
                _doc.Text(SpaceAfterMarker(piece.Text, "//", _options.SpaceBeforeTrailingCommentText), span, CommentFlags(piece));
                break;

            default:
                _doc.Text(piece.Text, span);
                break;
        }

        _lastPiece = index;
    }

    /// <summary>
    /// <c>place_comments_at_first_column = false</c> indents a comment with the code around it;
    /// true pins it to column 0, which is a habit some trees have and Skala honours rather than
    /// argues with.
    /// </summary>
    VerbatimFlags CommentFlags(Piece piece) =>
        _options.PlaceCommentsAtFirstColumn && piece.StartsLine ? VerbatimFlags.AtColumnZero : VerbatimFlags.None;

    /// <summary>
    /// <c>space_after_triple_slash</c> and <c>space_before_trailing_comment_text</c>: exactly one
    /// space after the marker, or the author's text untouched.
    /// </summary>
    static string SpaceAfterMarker(string text, string marker, bool required) {
        if (!text.StartsWith(marker, StringComparison.Ordinal)) {
            return text;
        }

        var body = text[marker.Length..];
        if (!required) {
            return text;
        }

        return body.Length == 0 || body[0] is ' ' or '\t' ? text : marker + " " + body;
    }

    static VerbatimFlags DirectiveFlags(PreprocessorIndentStyle style) =>
        style == PreprocessorIndentStyle.UsualIndent ? VerbatimFlags.None : VerbatimFlags.AtColumnZero;

    /// <summary>Every open frame has now seen a piece of its own.</summary>
    void MarkFramesStarted() {
        for (var i = _frames.Count - 1; i >= 0 && !_frames[i].Started; i--) {
            _frames[i] = _frames[i] with { Started = true };
            if (_frames[i].ResetsDepth) {
                _continuousDepth = 0;
            }
        }
    }

    void EmitFormatterOffSpan(int index) {
        // The escape hatch. It must work on the first attempt or people stop trusting the tool.
        var start = _pieces[index].Span.Start;
        var end = _source.Length;
        for (var i = index + 1; i < _pieces.Length; i++) {
            if (_pieces[i].IsComment && ContainsTag(_pieces[i].Text, _options.FormatterOnTag)) {
                end = _pieces[i].Span.End;
                break;
            }
        }

        EmitGap(index, PieceKind.LineComment, start, default);
        var span = new SourceSpan(start, end - start);
        _doc.Anchor(span, -1);
        _doc.Verbatim(_source[start..end], span);
        _verbatimUntil = end;
        _lastPiece = index;
    }

    bool ContainsTag(string text, string tag) =>
        !_options.FormatterTagsAcceptRegexp && text.Contains(tag, StringComparison.Ordinal);

    // ── Gaps ─────────────────────────────────────────────────────────────────────────────────

    void EmitGap(int nextPieceIndex, PieceKind nextKind, int nextStart, SyntaxToken nextToken) {
        if (_lastPiece < 0) {
            // Anything before the first piece is the file's prologue: a BOM, leading blank lines.
            // No anchor precedes it, so the emitter keeps it byte-for-byte.
            return;
        }

        var previous = _pieces[_lastPiece];
        var gap = _source[previous.Span.End..nextStart];
        var newLines = CountNewLines(gap);

        // ⚠ A gap that touches disabled text is copied byte-for-byte. Roslyn's DisabledTextTrivia
        // begins immediately after the opening directive's newline and swallows every blank line
        // next to it, so adding or removing one here does not move whitespace — it rewrites the
        // inactive branch, which Skala never does (docs/plan/04 § "Trivia").
        if (nextKind == PieceKind.DisabledText || previous.Kind == PieceKind.DisabledText && newLines > 0) {
            if (gap.Length > 0) {
                _doc.Verbatim(gap, new SourceSpan(previous.Span.End, gap.Length), VerbatimFlags.AtColumnZero);
            }

            return;
        }

        // A disabled block always ends with its own newline, so the whitespace between it and the
        // directive that closes the branch is not part of it and the directive indents normally.
        if (previous.Kind == PieceKind.DisabledText) {
            return;
        }

        // ⚠ The break plan only ever governs a gap between two tokens. A comment or a directive in
        // the gap makes it untouchable: joining `a + // note` with `b` puts `b` inside the comment,
        // and breaking before a directive moves code across it.
        var spec = default(GapSpec);
        var planned = previous.Kind == PieceKind.Token && nextKind == PieceKind.Token
            && _plan.TryGap(nextStart, out spec);

        if (planned) {
            switch (spec.Rule) {
                case GapRule.Point:
                    _doc.BreakPoint(
                        spec.Group,
                        GapSpace(previous, nextKind, nextToken) != SpaceKind.Forbidden,
                        newLines == 0 ? 0 : ResolveBlankLines(previous, nextPieceIndex, nextToken, newLines - 1),
                        newLines == 0
                            ? DefaultNewLine()
                            : _options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine());
                    return;

                case GapRule.Flat:
                    _doc.Space(GapSpace(previous, nextKind, nextToken));
                    return;

                default:
                    Break(
                        nextPieceIndex,
                        nextToken,
                        newLines == 0 ? 0 : ResolveBlankLines(previous, nextPieceIndex, nextToken, newLines - 1),
                        newLines == 0
                            ? DefaultNewLine()
                            : _options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine());
                    return;
            }
        }

        if (newLines == 0) {
            if (MustBreak(previous, nextKind, nextToken)) {
                Break(nextPieceIndex, nextToken, 0, DefaultNewLine());
                return;
            }

            _doc.Space(GapSpace(previous, nextKind, nextToken));
            return;
        }

        if (ShouldJoin(previous, nextKind, nextToken)) {
            _doc.Space(GapSpace(previous, nextKind, nextToken));
            return;
        }

        // ⚠ enforce_line_ending_style = false means an existing ending is kept, mixed endings
        // included; true normalises every break to end_of_line.
        Break(
            nextPieceIndex,
            nextToken,
            ResolveBlankLines(previous, nextPieceIndex, nextToken, newLines - 1),
            _options.EnforceLineEndingStyle ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine());
    }

    string DefaultNewLine() => _options.LineEnding switch {
        LineEnding.Crlf => "\r\n",
        LineEnding.Cr => "\r",
        _ => "\n"
    };

    /// <summary>
    /// Emits a break, spending the statement's one continuous indent level if this is the break
    /// that needs it.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>continuous_line_indent = single</c>: one level, and only where no delimited group is
    /// already providing one. <c>if (a &amp;&amp;\n b)</c> takes the parenthesis's level and not a
    /// second one; <c>var y = a\n + b</c> has no parenthesis and takes the statement's.
    /// </remarks>
    void Break(int nextPieceIndex, SyntaxToken nextToken, int blanks, string newLine) {
        var frame = FrameToSpend(nextPieceIndex, nextToken);
        if (frame >= 0) {
            OpenIndent(IndentKind.Continuous);
            _frames[frame] = _frames[frame] with { Activated = true };
        }

        _doc.Line(LineKind.Hard, blanks, newLine: newLine);
    }

    /// <summary>
    /// Which open frame, if any, pays for this break's continuation level.
    /// </summary>
    /// <remarks>
    /// ⚠ The walk goes outward, because a chain frame answers only for a break before its own
    /// <c>.</c>: in
    /// <code>
    /// public int M() =&gt;
    ///     Helper.Compute(x);
    /// </code>
    /// the innermost frame at the break is the chain, and the level is the <em>member's</em> to
    /// spend. Stopping at the innermost frame leaves the body flush with its declaration.
    /// </remarks>
    int FrameToSpend(int nextPieceIndex, SyntaxToken nextToken) {
        var beforeDot = nextToken.IsKind(SyntaxKind.DotToken)
            || nextToken.IsKind(SyntaxKind.QuestionToken) && nextToken.Parent is ConditionalAccessExpressionSyntax;

        for (var i = _frames.Count - 1; i >= 0; i--) {
            if (!_frames[i].Started) {
                continue;
            }

            if (_frames[i].Activated) {
                return -1;
            }

            if (_frames[i].Kind == FrameKind.Chain) {
                if (beforeDot) {
                    return i;
                }

                continue;
            }

            if (_frames[i].Kind == FrameKind.Pattern) {
                if (nextToken.Parent is BinaryPatternSyntax pattern && pattern.OperatorToken == nextToken) {
                    return i;
                }

                continue;
            }

            return _continuousDepth == 0 && IsContinuation(nextPieceIndex, nextToken) ? i : -1;
        }

        return -1;
    }

    enum FrameKind {
        Unit,
        Chain,
        Pattern
    }

    /// <param name="Started">
    /// ⚠ False until the frame's own first piece is emitted. A break that lands <em>before</em> a
    /// construct belongs to whatever encloses it, not to the construct: the break after
    /// <c>M() =&gt;</c> is the member's to pay for even though the lambda that follows has already
    /// been entered.
    /// </param>
    readonly record struct Frame(FrameKind Kind, bool Activated, bool Started = false, bool ResetsDepth = false);

    /// <summary>
    /// Whether the break continues an expression rather than starting a new statement, member or
    /// list element.
    /// </summary>
    bool IsContinuation(int nextPieceIndex, SyntaxToken nextToken) {
        if (nextToken.IsKind(SyntaxKind.None)) {
            // A comment or a directive takes the indentation of whatever it introduces.
            if (nextPieceIndex < 0) {
                return false;
            }

            for (var i = nextPieceIndex + 1; i < _pieces.Length; i++) {
                if (_pieces[i].Kind == PieceKind.Token) {
                    return IsContinuation(-1, _tokens[_pieces[i].TokenIndex]);
                }
            }

            return false;
        }

        return !StartsAUnit(nextToken);
    }

    /// <summary>
    /// True when the token begins something the layout treats as its own line: a statement, a
    /// member, a list element, a label, a clause — or a closing delimiter, which has already
    /// outdented by the time it is written.
    /// </summary>
    static bool StartsAUnit(SyntaxToken token) {
        if (token.Kind() is SyntaxKind.CloseBraceToken or SyntaxKind.CloseParenToken
            or SyntaxKind.CloseBracketToken or SyntaxKind.GreaterThanToken or SyntaxKind.OpenBraceToken) {
            return true;
        }

        SyntaxNode? child = null;
        for (var node = token.Parent; node is not null; node = node.Parent) {
            if (node.GetFirstToken() != token) {
                // ⚠ Two cases the "first token of a node" test misses, and both are common enough
                // to move the fidelity number on their own:
                //   an element of an initializer, whose enclosing node starts at the brace; and
                //   `[Test]` then `public void M()`, where the member's first token is the
                //   attribute's bracket but `public` still starts a line of its own. The second
                //   keeps walking rather than answering, because the node that carries the
                //   attributes may itself be an element of something.
                if (child is not null && IsListElement(child)) {
                    return true;
                }

                if (FirstTokenAfterAttributes(node) != token) {
                    return false;
                }
            }

            if (IsUnit(node)) {
                return true;
            }

            child = node;
        }

        return false;
    }

    static bool IsListElement(SyntaxNode child) =>
        child.Parent is InitializerExpressionSyntax or CollectionExpressionSyntax or BaseListSyntax;

    static SyntaxToken FirstTokenAfterAttributes(SyntaxNode node) {
        foreach (var element in node.ChildNodesAndTokens()) {
            if (element.IsNode && element.AsNode() is AttributeListSyntax) {
                continue;
            }

            return element.IsToken ? element.AsToken() : element.AsNode()!.GetFirstToken();
        }

        return node.GetFirstToken();
    }

    /// <summary>
    /// The things the layout treats as starting their own line: a statement, a member, a list
    /// element, a label, a clause.
    /// </summary>
    static bool IsUnit(SyntaxNode node) => node switch {
        StatementSyntax => true,
        MemberDeclarationSyntax => true,
        AccessorDeclarationSyntax => true,
        SwitchLabelSyntax => true,
        UsingDirectiveSyntax => true,
        ExternAliasDirectiveSyntax => true,
        AttributeListSyntax => true,
        ArgumentSyntax => true,
        AttributeArgumentSyntax => true,
        // ⚠ A parameter is a list element only in a real list. A simple lambda's single parameter
        // is the lambda's own first token, and treating it as an element makes `M() =>\n value =>`
        // sit flush with the member.
        ParameterSyntax { Parent: BaseParameterListSyntax } => true,
        TypeParameterSyntax => true,
        BaseTypeSyntax => true,
        TypeParameterConstraintClauseSyntax => true,
        SwitchExpressionArmSyntax => true,
        CatchClauseSyntax => true,
        FinallyClauseSyntax => true,
        ElseClauseSyntax => true,
        // ⚠ Every query clause starts a line of its own — except the first. `item =\n from p in xs`
        // is a continuation of the assignment and takes its level; `where` and `select` under it are
        // siblings of that `from` and take none. Treating the leading `from` as a unit too leaves the
        // whole query flush with the `=` and is 349 lines of the corpus's indentation divergence.
        FromClauseSyntax { Parent: QueryExpressionSyntax query } from when query.FromClause == from => false,
        QueryClauseSyntax => true,
        SelectOrGroupClauseSyntax => true,
        AnonymousObjectMemberDeclaratorSyntax => true,
        SubpatternSyntax => true,
        VariableDeclaratorSyntax => true,
        InitializerExpressionSyntax => true,
        CollectionElementSyntax => true,
        _ => false
    };

    SpaceKind GapSpace(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        // A trailing comment gets exactly one space before it (space_before_trailing_comment), and
        // its own text is left alone (space_before_trailing_comment_text = false).
        if (nextKind is PieceKind.LineComment or PieceKind.BlockComment
            or PieceKind.DocCommentLine or PieceKind.BlockDocComment) {
            return _options.SpaceBeforeTrailingComment ? SpaceKind.Required : SpaceKind.Forbidden;
        }

        if (previous.Kind != PieceKind.Token || nextKind != PieceKind.Token) {
            return SpaceKind.Required;
        }

        return SpaceRules.Decide(_tokens[previous.TokenIndex], nextToken, _options);
    }

    /// <summary>
    /// The brace rules, the one place phase 1 removes a line break the author wrote.
    /// </summary>
    /// <remarks>
    /// ⚠ Never across a comment or a directive. Joining <c>// note</c> with the <c>{</c> below it
    /// would put the brace inside the comment.
    /// </remarks>
    bool ShouldJoin(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        if (previous.Kind != PieceKind.Token || nextKind != PieceKind.Token) {
            return false;
        }

        var previousToken = _tokens[previous.TokenIndex];

        if (previousToken.IsKind(SyntaxKind.OpenBraceToken) && nextToken.IsKind(SyntaxKind.CloseBraceToken)) {
            return _options.EmptyBlockStyle == EmptyBlockStyle.Together && OpensAJoinableBody(previousToken);
        }

        if (nextToken.IsKind(SyntaxKind.OpenBraceToken)) {
            return _options.NewLineBeforeOpenBrace is "none" && OpensAJoinableBody(nextToken);
        }

        if (previousToken.IsKind(SyntaxKind.ElseKeyword)) {
            return nextToken.IsKind(SyntaxKind.IfKeyword) && _options.SpecialElseIfTreatment;
        }

        if (!previousToken.IsKind(SyntaxKind.CloseBraceToken)) {
            return false;
        }

        return nextToken.Kind() switch {
            SyntaxKind.ElseKeyword => !_options.NewLineBeforeElse,
            SyntaxKind.CatchKeyword => !_options.NewLineBeforeCatch,
            SyntaxKind.FinallyKeyword => !_options.NewLineBeforeFinally,
            SyntaxKind.WhileKeyword => nextToken.Parent is DoStatementSyntax && !_options.NewLineBeforeWhile,
            _ => false
        };
    }

    /// <summary>
    /// <c>allow_comment_after_lbrace = false</c>: a comment may not sit on the brace's line.
    /// </summary>
    bool MustBreak(Piece previous, PieceKind nextKind, SyntaxToken nextToken) {
        _ = nextToken;
        if (_options.AllowCommentAfterLbrace || previous.Kind != PieceKind.Token) {
            return false;
        }

        return nextKind is PieceKind.LineComment or PieceKind.DocCommentLine
            && _tokens[previous.TokenIndex].IsKind(SyntaxKind.OpenBraceToken);
    }

    static bool OpensAJoinableBody(SyntaxToken brace) =>
        brace.Parent is BlockSyntax or AccessorListSyntax or BaseTypeDeclarationSyntax
            or NamespaceDeclarationSyntax or SwitchStatementSyntax or InitializerExpressionSyntax
            or AnonymousObjectCreationExpressionSyntax or SwitchExpressionSyntax or PropertyPatternClauseSyntax;

    // ── Structure helpers ────────────────────────────────────────────────────────────────────

    static (SyntaxToken Open, SyntaxToken Close) BraceTokens(SyntaxNode node) => node switch {
        BlockSyntax block => (block.OpenBraceToken, block.CloseBraceToken),
        BaseTypeDeclarationSyntax type => (type.OpenBraceToken, type.CloseBraceToken),
        NamespaceDeclarationSyntax ns => (ns.OpenBraceToken, ns.CloseBraceToken),
        AccessorListSyntax accessors => (accessors.OpenBraceToken, accessors.CloseBraceToken),
        InitializerExpressionSyntax initializer => (initializer.OpenBraceToken, initializer.CloseBraceToken),
        AnonymousObjectCreationExpressionSyntax anonymous => (anonymous.OpenBraceToken, anonymous.CloseBraceToken),
        PropertyPatternClauseSyntax pattern => (pattern.OpenBraceToken, pattern.CloseBraceToken),
        SwitchExpressionSyntax switchExpression => (switchExpression.OpenBraceToken, switchExpression.CloseBraceToken),
        _ => FindDelimiters(node, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken)
    };

    static (SyntaxToken Open, SyntaxToken Close) DelimiterTokens(SyntaxNode node, NodeLayout layout) {
        var (openKind, closeKind) = layout switch {
            NodeLayout.Parens => (SyntaxKind.OpenParenToken, SyntaxKind.CloseParenToken),
            NodeLayout.Brackets => (SyntaxKind.OpenBracketToken, SyntaxKind.CloseBracketToken),
            _ => (SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken)
        };

        return FindDelimiters(node, openKind, closeKind);
    }

    static (SyntaxToken Open, SyntaxToken Close) FindDelimiters(SyntaxNode node, SyntaxKind openKind, SyntaxKind closeKind) {
        var open = default(SyntaxToken);
        var close = default(SyntaxToken);
        foreach (var child in node.ChildNodesAndTokens()) {
            if (!child.IsToken) {
                continue;
            }

            var token = child.AsToken();
            if (open.IsKind(SyntaxKind.None) && token.IsKind(openKind)) {
                open = token;
            } else if (token.IsKind(closeKind)) {
                close = token;
            }
        }

        return (open, close);
    }

    static StatementSyntax? EmbeddedStatement(SyntaxNode node) => node switch {
        IfStatementSyntax statement => statement.Statement,
        ElseClauseSyntax clause => clause.Statement,
        WhileStatementSyntax statement => statement.Statement,
        DoStatementSyntax statement => statement.Statement,
        ForStatementSyntax statement => statement.Statement,
        ForEachStatementSyntax statement => statement.Statement,
        ForEachVariableStatementSyntax statement => statement.Statement,
        UsingStatementSyntax statement => statement.Statement,
        FixedStatementSyntax statement => statement.Statement,
        LockStatementSyntax statement => statement.Statement,
        LabeledStatementSyntax statement => statement.Statement,
        _ => null
    };

    static (SyntaxToken Open, SyntaxToken Close) ConditionParentheses(SyntaxNode node) => node switch {
        IfStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        WhileStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        DoStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        ForStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        ForEachStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        ForEachVariableStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        UsingStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        FixedStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        LockStatementSyntax statement => (statement.OpenParenToken, statement.CloseParenToken),
        _ => (default, default)
    };

    /// <summary>
    /// ⚠ <c>indent_nested_{for,foreach,while,using,lock,fixed}_stmt = false</c>: a loop directly
    /// inside another loop of the same kind stays flush rather than stair-stepping. One of the few
    /// places the formatter <em>removes</em> indentation the author wrote.
    /// </summary>
    bool NeedsEmbeddedIndent(SyntaxNode owner, SyntaxNode embedded) {
        if (embedded is BlockSyntax) {
            return false;
        }

        // ⚠ special_else_if_treatment = true: `else if` is one line, not a nested block, so the
        // inner if takes no indent of its own.
        if (owner is ElseClauseSyntax && embedded is IfStatementSyntax && _options.SpecialElseIfTreatment) {
            return false;
        }

        var flush = owner switch {
            ForStatementSyntax => embedded is ForStatementSyntax && !_options.IndentNestedForStmt,
            ForEachStatementSyntax or ForEachVariableStatementSyntax =>
                embedded is ForEachStatementSyntax or ForEachVariableStatementSyntax && !_options.IndentNestedForeachStmt,
            WhileStatementSyntax => embedded is WhileStatementSyntax && !_options.IndentNestedWhileStmt,
            UsingStatementSyntax => embedded is UsingStatementSyntax && !_options.IndentNestedUsingsStmt,
            LockStatementSyntax => embedded is LockStatementSyntax && !_options.IndentNestedLockStmt,
            FixedStatementSyntax => embedded is FixedStatementSyntax && !_options.IndentNestedFixedStmt,
            _ => false
        };

        return !flush;
    }

    internal static int CountNewLines(string gap) {
        var count = 0;
        foreach (var c in gap) {
            if (c == '\n') {
                count++;
            }
        }

        return count;
    }

    static string? FirstNewLine(string gap) {
        for (var i = 0; i < gap.Length; i++) {
            if (gap[i] == '\r') {
                return i + 1 < gap.Length && gap[i + 1] == '\n' ? "\r\n" : "\r";
            }

            if (gap[i] == '\n') {
                return "\n";
            }
        }

        return null;
    }
}
