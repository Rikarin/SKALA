using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary><c>SK0240</c> — control flow that transfers to where control was already going.</summary>
/// <remarks>
///     <para>
///         Three shapes, one concept: a jump whose target is the next thing that would have happened
///         anyway, a <c>default:</c> section that only breaks, and a <c>catch</c> that only rethrows.
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
    ///     A <c>default:</c> section whose only statement is <c>break;</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>goto default;</c> anywhere in the same switch withdraws the finding. The section is the
    ///     jump's target, so deleting it turns a redundancy into CS0159 — a fix that does not compile is
    ///     the one failure a fixing tool may not have.
    /// </remarks>
    static void AnalyzeSwitch(SyntaxNodeAnalysisContext context) {
        var statement = (SwitchStatementSyntax)context.Node;
        foreach (var section in statement.Sections) {
            if (section.Labels.Count != 1
                || !section.Labels[0].IsKind(SyntaxKind.DefaultSwitchLabel)
                || section.Statements.Count != 1
                || !section.Statements[0].IsKind(SyntaxKind.BreakStatement)
                || !HasNoCommentOrDirective(section)
                || HasGotoDefault(statement)) {
                continue;
            }

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
    }

    static bool HasGotoDefault(SwitchStatementSyntax statement) {
        foreach (var node in statement.DescendantNodes()) {
            if (node.IsKind(SyntaxKind.GotoDefaultStatement)) {
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
        if (statement.Catches.Count == 0) {
            return;
        }

        var clause = statement.Catches[statement.Catches.Count - 1];
        if (clause.Filter is not null
            || clause.Block.Statements.Count != 1
            || clause.Block.Statements[0] is not ThrowStatementSyntax { Expression: null }
            || !HasNoCommentOrDirective(clause)) {
            return;
        }

        TextSpan span;
        string replacement;
        if (statement.Catches.Count > 1 || statement.Finally is not null) {
            span = clause.Span;
            replacement = string.Empty;
        } else if (CanUnwrap(statement)) {
            span = statement.Span;
            replacement = context.Node.SyntaxTree.GetText(context.CancellationToken)
                .ToString(
                    TextSpan.FromBounds(
                        statement.Block.OpenBraceToken.Span.End,
                        statement.Block.CloseBraceToken.SpanStart
                    )
                );
        } else {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                clause.CatchKeyword.GetLocation(),
                FixEdits.Pack((span, replacement)),
                "the `catch` only rethrows, so it handles nothing and resets nothing — the exception "
                + "propagates exactly as it would with no `catch` at all"
            )
        );
    }

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

    /// <summary>
    ///     ⚠ A comment inside the span withdraws the finding, because the fix deletes the span and the
    ///     comment with it. <c>default: break; // nothing to do for the rest</c> is the author answering
    ///     the question the reader was about to ask, and a cleanup that silently deletes prose has made
    ///     the file worse than it found it.
    /// </summary>
    static bool HasNoCommentOrDirective(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsDirective) {
                return false;
            }
        }

        return HasNoDirective(node);
    }
}
