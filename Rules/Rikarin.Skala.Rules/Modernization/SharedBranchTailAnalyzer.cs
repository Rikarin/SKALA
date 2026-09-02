using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1103</c> — both branches of an <c>if</c>/<c>else</c> end with the same statements.
/// </summary>
/// <remarks>
///     The shared tail is written twice and can only ever drift apart in one of the two places. Moving
///     it below the <c>if</c> states it once and leaves the branches holding only what actually
///     differs.
///     <para>
///         ⚠
///         <b>
///             The leading half of issue #108 is refuted rather than narrowed, and the reason is
///             evaluation order.
///         </b> A shared statement at the <em>top</em> of both branches can only be
///         hoisted <em>above</em> the <c>if</c>, where it now runs before the condition is evaluated
///         instead of after. <c>if (Advance()) { Log(); … } else { Log(); … }</c> and
///         <c>Log(); if (Advance()) …</c> are different programs whenever the condition or the shared
///         statement has an effect the other can see, and deciding that requires purity analysis of
///         two arbitrary expressions. The trailing half has no such problem: the destination is
///         immediately after the <c>if</c>, so nothing is reordered relative to anything.
///     </para>
///     <para>
///         ⚠ <b>An early jump inside a branch is safe and that took working through.</b> The worry is
///         <c>if (c) { if (d) break; Log(); } else { Other(); Log(); }</c> — the hoisted <c>Log()</c>
///         looks as though it becomes reachable on the <c>break</c> path. It does not: the statement
///         lands directly after the <c>if</c>, and every control transfer that skipped it inside the
///         branch — <c>return</c>, <c>throw</c>, <c>break</c>, <c>continue</c>, <c>goto</c>,
///         <c>yield break</c> — skips the position after the <c>if</c> just as it did the position
///         before it. That is why no jump appears in the guards below.
///     </para>
///     <para>
///         ⚠ <b>What the guards are actually about is names, and the check is syntactic on purpose.</b>
///         A statement moved out of a branch leaves that branch's scope, so anything it names which
///         the branch declared stops binding, and anything it <em>declares</em> moves outwards into
///         the enclosing block — the inward/outward scope move that produced <c>CS0136</c> in #304.
///         Rather than ask the model which symbol each name resolves to, every name either block
///         declares anywhere is collected and any mention of one in the moved statements withdraws the
///         finding. It over-bails — a name declared in the branch's own nested lambda counts, and
///         cannot conflict — and over-bailing costs findings where the alternative costs builds.
///     </para>
///     <para>
///         ⚠ <b>The whole shared run is one finding.</b> Reporting the last statement alone would fire
///         again on the next one after its own fix, which is the loop
///         <c>RuleFixtureTests.EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c> exists to catch.
///         The run is maximal, so the statements left behind are known to differ.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedBranchTailAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SharedBranchTail);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IfStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (IfStatementSyntax)context.Node;

        // ⚠ The destination has to exist. An `if` that is an embedded statement — the body of
        // another `if`, or an arm of an `else if` chain — has nowhere after it to put a statement
        // without inventing a block, which is a different rewrite.
        if (statement.Parent is not (BlockSyntax or SwitchSectionSyntax)
            || statement.Statement is not BlockSyntax then
            || statement.Else is not { Statement: BlockSyntax otherwise }) {
            return;
        }

        var shared = 0;
        while (shared < then.Statements.Count
               && shared < otherwise.Statements.Count
               && SyntaxFactory.AreEquivalent(
                   then.Statements[then.Statements.Count - 1 - shared],
                   otherwise.Statements[otherwise.Statements.Count - 1 - shared],
                   topLevel: false
               )) {
            shared++;
        }

        // ⚠ Both branches keep at least one statement. A branch consumed entirely says the `if` had
        // nothing to choose in the first place, which is a finding about the condition rather than
        // about the tail, and a fix that leaves `if (c) { } else { … }` behind is not this rule's.
        if (shared == 0 || shared >= then.Statements.Count || shared >= otherwise.Statements.Count) {
            return;
        }

        var declared = new HashSet<string>(System.StringComparer.Ordinal);
        Collect(then, declared);
        Collect(otherwise, declared);

        for (var i = then.Statements.Count - shared; i < then.Statements.Count; i++) {
            if (Names(then.Statements[i], declared)) {
                return;
            }
        }

        var tree = statement.SyntaxTree;
        var text = tree.GetText();

        var moved = Region(then, shared);
        var otherwiseMoved = Region(otherwise, shared);
        var kept = TextSpan.FromBounds(
            then.Statements[then.Statements.Count - shared].SpanStart,
            then.Statements[then.Statements.Count - 1].Span.End
        );

        // Both copies of the tail are deleted and one is written back. A comment in either copy is
        // therefore either duplicated or lost, and neither is a fix anybody can review.
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, moved)
            || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(tree, otherwiseMoved)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, kept),
                FixEdits.Pack(
                    (moved, string.Empty),
                    (otherwiseMoved, string.Empty),
                    (
                        new TextSpan(statement.Span.End, 0),
                        "\n" + StatementRewrites.IndentAt(text, statement.SpanStart) + text.ToString(kept)
                    )
                ),
                "Both branches end with the same "
                + (shared == 1
                        ? "statement"
                        : shared.ToString(System.Globalization.CultureInfo.InvariantCulture) + " statements")
            )
        );
    }

    /// <summary>The whole-line region the last <paramref name="shared" /> statements of a block own.</summary>
    static TextSpan Region(BlockSyntax block, int shared) =>
        TextSpan.FromBounds(
            block.Statements[block.Statements.Count - shared].FullSpan.Start,
            block.Statements[block.Statements.Count - 1].FullSpan.End
        );

    /// <summary>Every name a block introduces into a local scope, at any depth.</summary>
    static void Collect(BlockSyntax block, HashSet<string> into) {
        foreach (var node in block.DescendantNodes()) {
            foreach (var name in RewriteGuards.DeclaredNames(node)) {
                into.Add(name);
            }
        }
    }

    /// <summary>
    ///     Whether a statement being moved out of a branch mentions or introduces a name the branch
    ///     owns.
    /// </summary>
    static bool Names(StatementSyntax statement, HashSet<string> declared) {
        foreach (var node in statement.DescendantNodesAndSelf()) {
            // A declaration inside the moved statement escapes the branch with it, which is the
            // outward move C# answers with CS0136 whenever a sibling scope holds the same name.
            // ⚠ Whether there is *any* declared name, not which — the names themselves are unused here.
            if (RewriteGuards.DeclaredNames(node).Any()) {
                return true;
            }

            if (node is IdentifierNameSyntax identifier && declared.Contains(identifier.Identifier.ValueText)) {
                return true;
            }
        }

        return false;
    }
}
