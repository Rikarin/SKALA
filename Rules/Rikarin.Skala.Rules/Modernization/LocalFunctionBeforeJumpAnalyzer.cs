using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1102</c> — the block's <c>return</c> is written after the local functions it precedes.
/// </summary>
/// <remarks>
///     A block whose last statement is a jump, with one or more local function declarations sitting
///     directly above it, reads as though those declarations run before the method leaves. They do
///     not: a local function declaration is not an executable statement, and the jump is the last
///     thing the block does regardless of where it is written. Moving the jump above the run puts the
///     page in the order the program is in.
///     <para>
///         ⚠ <b>This rule is cosmetic and says so.</b> Nothing it reports is a defect and nothing it
///         changes affects behaviour, which is why it ships at <c>hint</c> — Roslyn <c>Hidden</c>,
///         reached only through <c>--include-hints</c> or an explicit severity. The argument for
///         spending an id on it is the one this repository exists for: the misordering is a shape
///         models emit constantly, because a local function is the last thing added to a method and
///         gets appended where the cursor was.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Hoisting is what makes the move sound, and it is a stronger fact than "the jump is
///             last".
///         </b> A local function may be <em>called</em> from above its own declaration, so a
///         <c>return Inner();</c> moved above <c>int Inner() => 7;</c> still binds. Nothing else in
///         the block moves, no scope changes, and no name is introduced or removed — this is the only
///         rewrite in the batch that cannot produce <c>CS0136</c> because it declares nothing.
///     </para>
///     <para>
///         ⚠ <b>Comments inside the local functions are untouched and that is deliberate.</b> The fix
///         inserts the jump at the start of the first local function's <em>full</em> span — above its
///         documentation comment rather than between the comment and the declaration — and deletes the
///         jump's own line. So the only comment at risk is one written above the jump itself, and that
///         one withdraws the finding.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalFunctionBeforeJumpAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.LocalFunctionBeforeJump);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Block);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var block = (BlockSyntax)context.Node;
        var statements = block.Statements;
        if (statements.Count < 2) {
            return;
        }

        var jump = statements[statements.Count - 1];
        if (!IsJump(jump) || statements[statements.Count - 2] is not LocalFunctionStatementSyntax) {
            return;
        }

        var first = statements.Count - 2;
        while (first > 0 && statements[first - 1] is LocalFunctionStatementSyntax) {
            first--;
        }

        var tree = block.SyntaxTree;
        var text = tree.GetText();
        var head = statements[first];

        // A comment above the jump belongs to the jump and would be left behind by the move.
        if (RewriteGuards.ContainsCommentOrDirective(tree, TextSpan.FromBounds(jump.FullSpan.Start, jump.SpanStart))) {
            return;
        }

        // ⚠ A directive anywhere in the region being reordered withdraws the finding. Comments do
        // not: the local functions keep their spans and their documentation with them. A `#if` is
        // different in kind — the jump could move out of a conditional block it was inside, or into
        // one it was not, and either is a second parse of the file rather than a reordering.
        for (var i = first; i < statements.Count; i++) {
            if (statements[i].ContainsDirectives) {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, TextSpan.FromBounds(jump.SpanStart, jump.Span.End)),
                FixEdits.Pack(
                    (
                        new TextSpan(head.FullSpan.Start, 0),
                        StatementRewrites.IndentAt(text, head.SpanStart)
                        + text.ToString(jump.Span)
                        + "\n"
                    ),
                    (RewriteGuards.LineSpanOf(jump), string.Empty)
                ),
                "The local function"
                + (statements.Count - 2 > first ? "s are" : " is")
                + " declared before the `"
                + jump.ChildTokens().First().ValueText
                + "` that ends the block"
            )
        );
    }

    /// <summary>Whether a statement leaves the block it ends without falling out of the bottom.</summary>
    /// <remarks>
    ///     ⚠ <c>goto</c> is not in the list. Every other jump here has one destination the reader can
    ///     see from the statement itself; a <c>goto</c>'s label may be anywhere in the member,
    ///     including above the local functions, and "the block ends here" is then not what the
    ///     statement says. <c>SK7074</c> already reports unstructured <c>goto</c> and this rule has
    ///     nothing to add to it.
    /// </remarks>
    static bool IsJump(StatementSyntax statement) =>
        statement is ReturnStatementSyntax
            or ThrowStatementSyntax
            or BreakStatementSyntax
            or ContinueStatementSyntax
            or YieldStatementSyntax;
}
