using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Correctness;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0240</c> — control flow that transfers to where control was already going.</summary>
/// <remarks>
///     <para>
///         Seven shapes, one concept: a jump whose target is the next thing that would have happened
///         anyway, a <c>default:</c> section that only breaks, a <c>case</c> label sharing its section
///         with <c>default:</c>, a <c>catch</c> that only rethrows, an empty <c>finally</c>, an
///         <c>else</c> on a branch that never falls through, and a switch-expression arm whose value
///         the arm below it already produces.
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
        context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
    }

    /// <summary>
    ///     An <c>else</c> hanging off a branch that never falls through.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>The condition is about the <em>then</em> branch, not the <c>else</c>.</b> When the then
    ///     branch always leaves — <c>return</c>, <c>throw</c>, <c>break</c>, <c>continue</c>,
    ///     <c>goto</c> — nothing can reach the statement after the <c>if</c> except the path that
    ///     already failed the condition, so <c>else</c> states a fact the control flow has already
    ///     established. It is the shape a model writes most: the two branches are symmetric in the
    ///     prompt, so they come out symmetric in the code.
    ///     <para>
    ///         ⚠ <b>The test is syntactic and therefore sound rather than complete.</b> A block whose
    ///         last statement is a jump cannot fall out of its end; a block ending in something else
    ///         may or may not, and is declined. Asking
    ///         <see cref="SemanticModel.AnalyzeControlFlow(SyntaxNode)" /> instead would find more, and
    ///         would make the whole of <c>SK0240</c> <c>requiresSemantics</c> — losing four shipped
    ///         shapes on every loose load to gain the fifth's tail.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>A directive anywhere inside the then branch withdraws the finding.</b>
    ///         <c>if (x) { #if TRACE return; #endif }</c> ends in a jump in one configuration and falls
    ///         through in the other, and the parse this analyzer sees is one of the two. This is the
    ///         only guard here that is about correctness rather than about text: the then branch is not
    ///         deleted, so a comment in it is safe and does not withdraw anything.
    ///     </para>
    /// </remarks>
    static void AnalyzeIf(SyntaxNodeAnalysisContext context) {
        var statement = (IfStatementSyntax)context.Node;

        // ⚠ A parent that is not a block cannot hold the extra statement an unwrap produces:
        // `while (c) if (a) return; else b();` has exactly one embedded statement position.
        // ⚠ There is deliberately no `HasNoDirective(clause)` here and the sabotage is what says so.
        // One was written, and removing it turned nothing red — because it cannot: that helper asks
        // about the clause's *leading and trailing* trivia, and the fix deletes from the `else` token
        // to the block's `{`, or from `else` to the embedded statement. A directive before `else` or
        // after the closing brace is in neither span, so the guard withdrew correct findings to
        // protect text nothing removes. Exactly #302's shape, and the span checks below cover what is
        // actually at risk.
        if (statement.Else is not { } clause
            || statement.Parent is not BlockSyntax
            || !AlwaysLeaves(statement.Statement)
            || HasDirectiveInside(statement.Statement)) {
            return;
        }

        var tree = context.Node.SyntaxTree;
        (TextSpan Span, string Text) edit;

        if (clause.Statement is BlockSyntax block) {
            // ⚠ Same escaping-locals rule as the sole-`catch` unwrap: a declaration, a local function
            // or a label inside the block belongs to the block's scope, and splicing the contents into
            // the enclosing block moves it out into a scope where it can collide.
            if (block.Statements.Count == 0 || DeclaresSomething(block)) {
                return;
            }

            // The braces and the keyword are what the splice deletes; the contents survive verbatim,
            // so a comment inside the block must not withdraw the finding (#302).
            if (RewriteGuards.ContainsCommentOrDirective(
                    tree,
                    TextSpan.FromBounds(clause.SpanStart, block.OpenBraceToken.Span.End)
                )
                || RewriteGuards.ContainsCommentOrDirective(
                    tree,
                    TextSpan.FromBounds(block.CloseBraceToken.SpanStart, clause.Span.End)
                )) {
                return;
            }

            edit = (
                clause.Span,
                tree.GetText(context.CancellationToken)
                    .ToString(
                        TextSpan.FromBounds(block.OpenBraceToken.Span.End, block.CloseBraceToken.SpanStart)
                    )
            );
        } else {
            // `else foo();` and `else if (…)` need no unwrap at all: only the keyword goes, and the
            // embedded statement keeps whatever scope it had.
            var span = TextSpan.FromBounds(clause.SpanStart, clause.Statement.SpanStart);
            if (RewriteGuards.ContainsCommentOrDirective(tree, span)) {
                return;
            }

            edit = (span, string.Empty);
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                clause.ElseKeyword.GetLocation(),
                FixEdits.Pack(edit),
                "the `if` branch always leaves, so the statements after it are already the else case — "
                + "`else` narrows nothing"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether control can reach the end of <paramref name="statement" />, answered on syntax.
    /// </summary>
    /// <remarks>
    ///     Sound in the direction that matters: a <c>true</c> here is always right, because a jump is
    ///     the last thing that runs. <c>false</c> is returned for everything else, including bodies
    ///     that in fact never fall through — an <c>if</c>/<c>else</c> where both arms return, a
    ///     <c>while (true)</c> with no <c>break</c>, an exhaustive <c>switch</c>. Those are missed
    ///     findings, not wrong ones.
    /// </remarks>
    static bool AlwaysLeaves(StatementSyntax statement) => statement switch {
        ReturnStatementSyntax or ThrowStatementSyntax => true,
        BreakStatementSyntax or ContinueStatementSyntax or GotoStatementSyntax => true,
        BlockSyntax { Statements.Count: > 0 } block => AlwaysLeaves(block.Statements[block.Statements.Count - 1]),
        _ => false
    };

    static bool DeclaresSomething(BlockSyntax block) {
        foreach (var inner in block.Statements) {
            if (inner is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Directives only — a comment inside a span the fix keeps is not a hazard.
    /// </summary>
    static bool HasDirectiveInside(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsDirective) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     A switch-expression arm whose value the arm below it already produces.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Measured against ReSharper 2025.2.6 rather than guessed at.</b> Four candidate readings
    ///     of <c>RedundantSwitchExpressionArms</c> were put through <c>jb inspectcode</c>:
    ///     <c>b switch { true => 1, false => 2, _ => 3 }</c> is <b>CS8510, a compiler error</b>, not a
    ///     lint finding; a non-exhaustive switch whose arms all agree is <b>CS8509</b> and is not
    ///     reported either; what <em>is</em> reported is an arm whose expression the trailing arm
    ///     repeats — <c>n switch { 1 =&gt; "a", 2 =&gt; "b", _ =&gt; "b" }</c> flags <c>2 =&gt; "b"</c>.
    ///     <para>
    ///         ⚠ <b>Only the unbroken run of agreeing arms directly above the discard, and that is the
    ///         correctness argument rather than an economy.</b> Deleting arm <em>i</em> is safe only if
    ///         everything matching its pattern lands on an arm producing the same value, which is
    ///         exactly "every arm below it, down to the discard, agrees". A scan that reported any arm
    ///         equal to the last one would be wrong on <c>{ 1 =&gt; "a", 2 =&gt; "b", _ =&gt; "a" }</c>,
    ///         where deleting <c>1 =&gt; "a"</c> happens to be right but only because <c>1</c> does not
    ///         match <c>2</c> — a fact this rule does not attempt to know.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The whole run is one finding carrying one edit per arm, and both halves of that
    ///         shape are forced by a test rather than chosen.</b> Reporting each arm separately fails
    ///         <c>CleanupBatchTests.EveryFixture_ProducesTheExactCount</c>, which exists because a rule
    ///         reporting one redundancy twice gives <c>skala fix</c> two edits for one finding.
    ///         Reporting only the lowest arm and leaving the rest to the next pass fails
    ///         <c>FixRoundTripTests</c>, because the fix's own output still carries the finding it
    ///         uncovered — the "convergent sequence" argument that works for the <c>try</c> shapes next
    ///         door does <em>not</em> transfer here, and it was written down before it was measured.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>The whole-switch collapse is deliberately not offered.</b> ReSharper also reports
    ///         "redundant arm<em>s</em>" plural where every arm agrees, and its fix replaces the switch
    ///         with the value — which stops evaluating the governing expression. Here the arms go one at
    ///         a time and <c>n switch { _ =&gt; "a" }</c> is where it stops: still a switch, still
    ///         evaluating <c>n</c>, and nothing has been decided on the author's behalf.
    ///     </para>
    /// </remarks>
    static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context) {
        var expression = (SwitchExpressionSyntax)context.Node;
        var arms = expression.Arms;
        if (arms.Count < 2) {
            return;
        }

        var last = arms[arms.Count - 1];

        // ⚠ The trailing arm has to match everything the arms above it could have matched, or deleting
        // one of them changes which arm runs. `_ when c` is not total and neither is any pattern that
        // tests a value.
        if (last.Pattern is not DiscardPatternSyntax || last.WhenClause is not null) {
            return;
        }

        // ⚠ The run of agreeing arms directly above the discard is ONE finding carrying one edit per
        // arm, and both halves of that are forced by a test. Reporting each arm separately fails
        // `CleanupBatchTests.EveryFixture_ProducesTheExactCount`; reporting only the lowest fails
        // `FixRoundTripTests.ApplyingAFix_LeavesTheCodeCompilingAndTheRuleSilent`, because the fix's
        // own output still carries the finding it uncovered. The composite edit is the only shape that
        // is neither.
        var tree = context.Node.SyntaxTree;
        var edits = new List<(TextSpan Span, string Text)>();
        var topmost = last;

        for (var index = arms.Count - 2; index >= 0; index--) {
            var arm = arms[index];

            // ⚠ A `when` clause is an expression that runs, so an arm carrying one is not inert even
            // when its value agrees; and a pattern that binds a name makes two syntactically identical
            // expressions mean different things.
            if (arm.WhenClause is not null
                || BindsAName(arm.Pattern)
                || !SyntaxFactory.AreEquivalent(arm.Expression, last.Expression, topLevel: false)) {
                break;
            }

            // ⚠ The span starts at the arm's first token, so a comment on the line *above* it is not
            // in what the fix deletes and does not withdraw the finding — #302's lesson, and there is
            // a positive fixture asserting exactly that.
            var span = TextSpan.FromBounds(arm.SpanStart, arms.GetSeparator(index).Span.End);
            if (RewriteGuards.ContainsCommentOrDirective(tree, span)) {
                break;
            }

            edits.Add((span, string.Empty));
            topmost = arm;
        }

        if (edits.Count == 0) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                topmost.GetLocation(),
                FixEdits.Pack([.. edits]),
                edits.Count == 1
                    ? "the arm below produces the same value for everything this pattern matches, so "
                    + "the arm changes no result"
                    : $"the arm below produces the same value for everything these {edits.Count} "
                    + "patterns match, so the arms change no result"
            )
        );
    }

    /// <summary>Whether the pattern introduces a name its arm's expression could be reading.</summary>
    static bool BindsAName(PatternSyntax pattern) {
        foreach (var node in pattern.DescendantNodesAndSelf()) {
            if (node is SingleVariableDesignationSyntax) {
                return true;
            }
        }

        return false;
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
                    && (section.Labels.Count == 1 || !HasGoto(statement, SyntaxKind.GotoCaseStatement))
                    && !IsWhatKeepsSK2009Quiet(context, statement, section)) {
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
    ///     ⚠ Whether this empty <c>default:</c> section is the only thing keeping <c>SK2009</c> quiet.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             This rule and <c>SK2009</c> read the same construct in opposite directions, and
    ///             without this guard they are a fix loop.
    ///         </b> <c>SK2009</c> counts a <c>default:</c>
    ///         section as the catch-all that legitimises a non-exhaustive enum switch; this rule counts
    ///         an <em>empty</em> one as dead control flow and offers to delete it. Deleting it cleared
    ///         the <c>SK0240</c> and immediately produced <c>SK2009: switch over `DocKind` omits …</c> at
    ///         the same switch ([#321]) — a fix that hands the author a finding they did not have.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>This rule stands down and <c>SK2009</c> keeps the shape</b>, on the ground that
    ///         decides which of two defensible readings is right: where members are unhandled the
    ///         section is <em>not</em> dead. It is the author's written statement that the rest of the
    ///         enum is deliberately ignored, which is exactly the signal <c>SK2009</c> reads, so
    ///         deleting it removes information rather than removing nothing. The alternative — report
    ///         with no fix — de-automates the contradiction without settling it: an author taking the
    ///         advice by hand still lands on the <c>SK2009</c>.
    ///     </para>
    ///     <para>
    ///         ⚠ <b>Narrow by construction.</b> The question asked is <c>SK2009</c>'s own predicate with
    ///         this section removed, so an <em>exhaustive</em> enum switch's empty <c>default:</c> is
    ///         still reported, and so is one over <c>int</c>, over <c>SyntaxKind</c> where the switch is
    ///         a minority filter, and over type patterns. Those are the shapes the #321 batch deleted
    ///         and they stay deleted.
    ///     </para>
    ///     <para>
    ///         ⚠ The answer is opportunistic rather than required: this rule is <c>scope: Syntax</c> and
    ///         runs in loose mode, where an enum from an unreferenced assembly does not resolve and the
    ///         guard says nothing. <c>SK2009</c> is <c>requiresSemantics</c> and is not running there
    ///         either, so the two still agree about the file in front of them.
    ///     </para>
    /// </remarks>
    static bool IsWhatKeepsSK2009Quiet(
        SyntaxNodeAnalysisContext context,
        SwitchStatementSyntax statement,
        SwitchSectionSyntax section
    ) =>
        EnumSwitchCoverage.Gap(
            context.SemanticModel,
            statement,
            context.Compilation.GetTypeByMetadataName("System.FlagsAttribute"),
            section,
            context.CancellationToken
        ) is not null;

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
