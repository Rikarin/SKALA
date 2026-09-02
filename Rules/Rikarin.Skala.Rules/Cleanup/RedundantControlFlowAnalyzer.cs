using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0240</c> — control flow that transfers to where control was already going.</summary>
/// <remarks>
///     <para>
///         Five shapes, one concept: a jump whose target is the next thing that would have happened
///         anyway, a <c>default:</c> section that only breaks, a <c>case</c> label sharing its section
///         with <c>default:</c>, a <c>catch</c> that only rethrows, and an empty <c>finally</c>.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The comment guards ask about the span the fix <em>deletes</em>, never about the node's
///             leading trivia, and getting that wrong cost this rule two of its own shapes.
///         </b> The
///         first version asked <c>DescendantTrivia</c> of the <c>catch</c> clause and of the
///         <c>default:</c> section — and a node's descendant trivia begins with its first token's
///         <em>leading</em> trivia, which is the comment written on the line <em>above</em> it. The fix
///         deletes <c>clause.Span</c> and <c>section.Span</c>, both of which start at the keyword, so
///         that comment was never at risk; the guard withdrew a correct finding to protect text the fix
///         does not touch. Measured, not read: a <c>// deliberate</c> above the <c>catch</c> took the
///         count from 1 to 0, and the same above <c>default:</c> did too. This is the shape recorded as
///         [#302], and the two branches here now ask
///         <see cref="RewriteGuards.ContainsCommentOrDirective(SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan)" />
///         over the deleted span instead. ⚠ The sibling rules were probed the same way and are clean:
///         <c>SK0241</c> deletes from a keyword to the next token and guards only that keyword's
///         <em>trailing</em> trivia, and <c>SK0244</c> deletes a declaration's <em>full</em> span, so
///         its leading-trivia guard covers exactly what it removes.
///     </para>
///     <para>
///         ⚠ <b>The <c>catch (X) { throw; }</c> member is the one that matters</b> and the other two are
///         housekeeping beside it. A rethrowing catch is not noise: it reads as error handling, it is
///         where a reader goes looking for the recovery, the wrapping or the logging — and there is
///         none. Every question it raises has the answer "nothing happens here", which is exactly what
///         the code would say if the clause were absent. `throw;` also preserves the stack trace, so
///         unlike <c>SK2015</c>'s <c>throw ex;</c> there is not even a defect to find underneath: the
///         clause is inert.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Only the last <c>catch</c> of a <c>try</c> is ever reported, and that is a correctness
///             constraint rather than caution.
///         </b> Deleting an earlier one changes which handler an
///         exception reaches: in
///         <c>try { … } catch (IOException) { throw; } catch (Exception e) { Log(e); }</c> the first
///         clause is what stops an <c>IOException</c> being logged, so removing it is a behaviour
///         change wearing a redundancy's clothes. Removing the <em>last</em> clause can only ever leave
///         the exception propagating, which is what the rethrow already did.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantControlFlowAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantControlFlow);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeJump, SyntaxKind.ReturnStatement, SyntaxKind.ContinueStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitch, SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(AnalyzeTry, SyntaxKind.TryStatement);
    }

    static void AnalyzeJump(SyntaxNodeAnalysisContext context) {
        var statement = (StatementSyntax)context.Node;

        // ⚠ Last statement of the block, and the block has to *be* the construct's body. A `return;`
        // that ends an `if` block is not the end of the method and deleting it would change what runs.
        if (statement.Parent is not BlockSyntax block
            || block.Statements[block.Statements.Count - 1] != statement
            || !HasNoDirective(statement)) {
            return;
        }

        var what = statement switch {
            ContinueStatementSyntax when IsLoopBody(block) =>
                "`continue;` is the last statement of the loop body, so control reaches the next iteration "
                + "either way",
            ReturnStatementSyntax { Expression: null } when EndsAVoidBody(block) =>
                "`return;` is the last statement of a body that returns nothing, so control leaves here either way",
            _ => null
        };

        if (what is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                FixEdits.Pack((statement.Span, string.Empty)),
                what
            )
        );
    }

    /// <summary>Whether the block is the embedded body of a loop, so falling off its end continues it.</summary>
    /// <remarks>
    ///     ⚠ All four loops, including <c>do</c>: a <c>continue</c> there jumps to the condition, and so
    ///     does falling off the end of the body. A <c>for</c> loop's incrementor runs on both paths too.
    /// </remarks>
    static bool IsLoopBody(BlockSyntax block) =>
        block.Parent switch {
            ForStatementSyntax loop => loop.Statement == block,
            ForEachStatementSyntax loop => loop.Statement == block,
            ForEachVariableStatementSyntax loop => loop.Statement == block,
            WhileStatementSyntax loop => loop.Statement == block,
            DoStatementSyntax loop => loop.Statement == block,
            _ => false
        };

    /// <summary>
    ///     Whether the block is the whole body of something that produces no value.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read from the written return type, never from a symbol: this rule runs in loose mode, and
    ///     <c>void</c> is a keyword that cannot be aliased or shadowed. An <c>async Task</c> body is the
    ///     same redundancy and is deliberately not matched — the name <c>Task</c> can be aliased, so
    ///     answering it needs a model this rule does not ask for.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             There is no iterator guard, and the one that was written here has been removed as
    ///             unreachable.
    ///         </b> The belief it encoded — that a bare <c>return;</c> in an iterator is legal
    ///         and means <c>yield break</c> — is false: the compiler rejects it with CS1622, measured on
    ///         a fixture that was committed as a negative case and failed to compile. An iterator cannot
    ///         contain the shape this rule matches, so nothing has to exclude it.
    ///     </para>
    /// </remarks>
    static bool EndsAVoidBody(BlockSyntax block) =>
        block.Parent switch {
            MethodDeclarationSyntax method => method.Body == block && IsVoid(method.ReturnType),
            LocalFunctionStatementSyntax local => local.Body == block && IsVoid(local.ReturnType),
            ConstructorDeclarationSyntax constructor => constructor.Body == block,
            DestructorDeclarationSyntax destructor => destructor.Body == block,
            AccessorDeclarationSyntax accessor => accessor.Body == block && ReturnsNothing(accessor),
            _ => false
        };

    static bool IsVoid(TypeSyntax type) =>
        type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    static bool ReturnsNothing(AccessorDeclarationSyntax accessor) =>
        accessor.Keyword.IsKind(SyntaxKind.SetKeyword)
        || accessor.Keyword.IsKind(SyntaxKind.InitKeyword)
        || accessor.Keyword.IsKind(SyntaxKind.AddKeyword)
        || accessor.Keyword.IsKind(SyntaxKind.RemoveKeyword);

    /// <summary>
    ///     Two shapes in one <c>switch</c>: a <c>default:</c> section whose only statement is
    ///     <c>break;</c>, and a <c>case</c> label sharing its section with <c>default:</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>goto default;</c> anywhere in the same switch withdraws the first finding and
    ///     <c>goto case X;</c> withdraws the second. The label is the jump's target, so deleting it
    ///     turns a redundancy into CS0159 — a fix that does not compile is the one failure a fixing
    ///     tool may not have.
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             The two shapes are exclusive, the whole-section one wins, and that ordering is what
    ///             makes the fix converge in one pass.
    ///         </b> A section that only breaks is deleted entire —
    ///         extra <c>case</c> labels and all — rather than having its labels reported one at a
    ///         time, because deleting the <c>case 2:</c> of <c>case 2: default: break;</c> leaves
    ///         <c>default: break;</c>, which is this rule's <em>other</em> switch shape: the fix's own
    ///         output would still carry a finding. Deleting the section answers both at once and is
    ///         behaviour-preserving for the same reason the single-label case is — every value the
    ///         section named now falls off the end of the switch, which is what <c>break</c> did. The
    ///         label branch therefore only ever sees a section that does real work.
    ///     </para>
    /// </remarks>
    static void AnalyzeSwitch(SyntaxNodeAnalysisContext context) {
        var statement = (SwitchStatementSyntax)context.Node;
        var tree = context.Node.SyntaxTree;
        foreach (var section in statement.Sections) {
            if (!HasDefaultLabel(section)) {
                continue;
            }

            // ⚠ The whole section goes first, extra labels and all, and that ordering is what makes
            // the fix converge in one pass. Reporting the `case` label of `case 2: default: break;`
            // leaves `default: break;` behind — which is this rule's *other* switch shape, so the
            // fix's own output still carries a finding. Deleting the section answers both at once,
            // and it is behaviour-preserving for the same reason the single-label case is: every
            // value the section named now falls off the end of the switch, which is what `break` did.
            if (section.Statements.Count == 1 && section.Statements[0].IsKind(SyntaxKind.BreakStatement)) {
                if (!RewriteGuards.ContainsCommentOrDirective(tree, section.Span)
                    && HasNoDirective(section)
                    && !HasGoto(statement, SyntaxKind.GotoDefaultStatement)
                    && (section.Labels.Count == 1 || !HasGoto(statement, SyntaxKind.GotoCaseStatement))) {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptor,
                            section.GetLocation(),
                            FixEdits.Pack((section.Span, string.Empty)),
                            "the `default:` section only breaks, which is what a `switch` with no matching section "
                            + "already does"
                        )
                    );
                }

                continue;
            }

            // ⚠ Any `goto case` withdraws every label in the switch rather than only the matching
            // one. Matching the jump's expression to the label's would mean comparing two constant
            // expressions without a semantic model — `goto case Colour.Red;` against `case Red:` —
            // and being wrong there produces CS0159 from a fix marked safe.
            if (HasGoto(statement, SyntaxKind.GotoCaseStatement)) {
                continue;
            }

            foreach (var label in section.Labels) {
                if (label.IsKind(SyntaxKind.DefaultSwitchLabel)
                    || RewriteGuards.ContainsCommentOrDirective(tree, LabelSpan(label))
                    || !HasNoDirective(label)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        label.GetLocation(),
                        FixEdits.Pack((LabelSpan(label), string.Empty)),
                        "the label shares its section with `default:`, which every value reaches anyway, so "
                        + "naming this one selects nothing"
                    )
                );
            }
        }
    }

    /// <summary>
    ///     The label and the whitespace up to the next token, so deleting it does not orphan a line.
    /// </summary>
    static TextSpan LabelSpan(SwitchLabelSyntax label) =>
        TextSpan.FromBounds(label.SpanStart, label.GetLastToken().GetNextToken().SpanStart);

    static bool HasDefaultLabel(SwitchSectionSyntax section) {
        foreach (var label in section.Labels) {
            if (label.IsKind(SyntaxKind.DefaultSwitchLabel)) {
                return true;
            }
        }

        return false;
    }

    static bool HasGoto(SwitchStatementSyntax statement, SyntaxKind kind) {
        foreach (var node in statement.DescendantNodes()) {
            if (node.IsKind(kind)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The last <c>catch</c> of a <c>try</c>, unfiltered, whose body is exactly <c>throw;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two fixes, and which one applies is decided by what would be left behind. Where another
    ///     <c>catch</c> or a <c>finally</c> remains, the clause is simply deleted. Where it is the only
    ///     one, <c>try { … }</c> alone is CS1524, so the whole statement has to be replaced by its
    ///     block's contents — and that is only offered when nothing in the block <em>declares</em>
    ///     anything, because unwrapping a scope moves every local it holds out into the enclosing one,
    ///     where it can collide with a name that was never in conflict. A try block that declares a
    ///     local is left alone rather than fixed badly.
    /// </remarks>
    static void AnalyzeTry(SyntaxNodeAnalysisContext context) {
        var statement = (TryStatementSyntax)context.Node;
        if (ReportRethrowingCatch(context, statement)) {
            return;
        }

        ReportEmptyFinally(context, statement);
    }

    /// <summary>Whether the statement's last <c>catch</c> was reported as an inert rethrow.</summary>
    static bool ReportRethrowingCatch(SyntaxNodeAnalysisContext context, TryStatementSyntax statement) {
        if (statement.Catches.Count == 0) {
            return false;
        }

        var clause = statement.Catches[statement.Catches.Count - 1];
        if (clause.Filter is not null
            || clause.Block.Statements.Count != 1
            || clause.Block.Statements[0] is not ThrowStatementSyntax { Expression: null }
            || !HasNoDirective(clause)) {
            return false;
        }

        var tree = context.Node.SyntaxTree;
        if (RewriteGuards.ContainsCommentOrDirective(tree, clause.Span)) {
            return false;
        }

        // ⚠ An empty `finally` on the same `try` is the *other* shape of this rule, and the two have
        // to be answered together. Reported separately they are two findings whose edits compose into
        // `try { … }` — CS1524 — and reported one-per-pass the fix's own output still carries a
        // finding, which is what `FixRoundTripTests` calls an edit that did not address the finding.
        // So the clause that survives decides one composite edit here.
        var emptyFinally = IsDeletableEmptyFinally(tree, statement) ? statement.Finally : null;

        (TextSpan Span, string Text)[] edits;
        if (statement.Catches.Count > 1) {
            edits = emptyFinally is null
                ? [(clause.Span, string.Empty)]
                : [(clause.Span, string.Empty), (emptyFinally.Span, string.Empty)];
        } else if (statement.Finally is not null && emptyFinally is null) {
            // A `finally` that does work survives, so `try { … } finally { … }` still parses.
            edits = [(clause.Span, string.Empty)];
        } else if (CanUnwrap(statement) && !LosesText(tree, statement)) {
            edits = [(statement.Span, BlockContents(context, statement))];
        } else {
            return false;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                clause.CatchKeyword.GetLocation(),
                FixEdits.Pack(edits),
                "the `catch` only rethrows, so it handles nothing and resets nothing — the exception "
                + "propagates exactly as it would with no `catch` at all"
            )
        );

        return true;
    }

    /// <summary>Whether the statement's <c>finally</c> is one this rule may delete.</summary>
    static bool IsDeletableEmptyFinally(SyntaxTree tree, TryStatementSyntax statement) =>
        statement.Finally is { Block.Statements.Count: 0 } clause
        && HasNoDirective(clause)
        && !RewriteGuards.ContainsCommentOrDirective(tree, clause.Span);

    /// <summary>The text between the try block's braces, which is what an unwrap leaves behind.</summary>
    static string BlockContents(SyntaxNodeAnalysisContext context, TryStatementSyntax statement) =>
        context.Node.SyntaxTree.GetText(context.CancellationToken)
            .ToString(
                TextSpan.FromBounds(
                    statement.Block.OpenBraceToken.Span.End,
                    statement.Block.CloseBraceToken.SpanStart
                )
            );

    /// <summary>
    ///     A <c>finally { }</c> with nothing in it.
    /// </summary>
    /// <remarks>
    ///     ⚠
    ///     <b>
    ///         An empty <c>finally</c> is the <c>catch</c>'s mirror image and it is the member of
    ///         [#131]'s thirteen that the shipped rule's own accounting had lost.
    ///     </b> It reads as a
    ///     guarantee — this runs whatever happens — and there is nothing to run, so every question it
    ///     raises has the answer "nothing happens here".
    ///     <para>
    ///         ⚠ <b>Never reported on a <c>try</c> whose <c>catch</c> was already reported</b>, and that
    ///         is a correctness constraint rather than tidiness. The two edits are separate spans, so
    ///         nothing stops <c>skala fix</c> applying both — and applying both to
    ///         <c>try { A } catch (E) { throw; } finally { }</c> leaves <c>try { A }</c>, which is
    ///         CS1524. One finding per <c>try</c> per pass; the second shape is reported by the next
    ///         pass over the fixed text, which is a convergent sequence rather than a loop.
    ///     </para>
    ///     <para>
    ///         ⚠ Where the <c>finally</c> is the only clause the whole statement has to be replaced by
    ///         its block's contents, under exactly <see cref="CanUnwrap" />'s conditions — the same
    ///         CS1524 and the same escaping locals as the sole-<c>catch</c> case.
    ///     </para>
    /// </remarks>
    static void ReportEmptyFinally(SyntaxNodeAnalysisContext context, TryStatementSyntax statement) {
        var tree = context.Node.SyntaxTree;
        if (!IsDeletableEmptyFinally(tree, statement) || statement.Finally is not { } clause) {
            return;
        }

        (TextSpan Span, string Text) edit;
        if (statement.Catches.Count > 0) {
            edit = (clause.Span, string.Empty);
        } else if (CanUnwrap(statement) && !LosesText(tree, statement)) {
            edit = (statement.Span, BlockContents(context, statement));
        } else {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                clause.FinallyKeyword.GetLocation(),
                FixEdits.Pack(edit),
                "the `finally` block is empty, so it guarantees that nothing happens — which is what "
                + "happens with no `finally` at all"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether unwrapping a <c>try</c> would delete text a person wrote.
    /// </summary>
    /// <remarks>
    ///     The unwrap keeps the block's <em>contents</em> verbatim, so a comment inside the block
    ///     survives and must not withdraw the finding — checking the whole replaced span, which is what
    ///     the deletion cases check, would silence the rule on every commented <c>try</c> body. What is
    ///     actually lost is the header up to the block's <c>{</c> and the tail from its <c>}</c>
    ///     onwards, so those two spans are the ones asked about.
    /// </remarks>
    static bool LosesText(SyntaxTree tree, TryStatementSyntax statement) =>
        RewriteGuards.ContainsCommentOrDirective(
            tree,
            TextSpan.FromBounds(statement.SpanStart, statement.Block.OpenBraceToken.Span.End)
        )
        || RewriteGuards.ContainsCommentOrDirective(
            tree,
            TextSpan.FromBounds(statement.Block.CloseBraceToken.SpanStart, statement.Span.End)
        );

    /// <summary>
    ///     Whether <c>try { A } catch { throw; }</c> may be replaced by <c>A</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three conditions, each of which is a way the splice would otherwise be wrong: an empty
    ///     block would delete an embedded statement out of an <c>if</c> and leave text that does not
    ///     parse; a parent that is not a block cannot hold more than one statement; and a declaration,
    ///     a local function or a label inside the block belongs to the block's scope and would escape
    ///     into the enclosing one.
    /// </remarks>
    static bool CanUnwrap(TryStatementSyntax statement) {
        if (statement.Block.Statements.Count == 0 || statement.Parent is not BlockSyntax) {
            return false;
        }

        foreach (var inner in statement.Block.Statements) {
            if (inner is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     ⚠ A directive in the trivia makes the deleted span's ownership a question, so it is not
    ///     deleted.
    /// </summary>
    static bool HasNoDirective(SyntaxNode node) {
        foreach (var trivia in node.GetLeadingTrivia()) {
            if (trivia.IsDirective) {
                return false;
            }
        }

        foreach (var trivia in node.GetTrailingTrivia()) {
            if (trivia.IsDirective) {
                return false;
            }
        }

        return true;
    }
}
