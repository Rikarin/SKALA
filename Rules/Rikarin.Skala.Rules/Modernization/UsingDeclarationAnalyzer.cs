using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
/// <c>SK1006</c> — a <c>using</c> statement whose block runs to the end of its scope is a
/// <c>using</c> declaration.
/// </summary>
/// <remarks>
/// ⚠ This rule moves a <c>Dispose</c>, which is why M5 did not ship it, and the guard is the whole
/// rule. A <c>using</c> declaration disposes when its enclosing block ends, so the rewrite is
/// behaviour-preserving exactly when the <c>using</c> statement is the <b>last statement of its
/// enclosing block</b>: at that point the block's closing brace and the <c>using</c> block's
/// closing brace are the same program point, and the object is disposed at the same instant on
/// every path out — <c>return</c>, <c>throw</c>, <c>break</c> and falling off the end alike.
/// Anywhere else the object starts living longer, and "disposed later than it was" is not a
/// formatting difference.
/// <para>
/// ⚠ The second guard is scoping, and it is the one that is easy to get subtly wrong. Removing the
/// braces lifts the block's own declaration space one scope outwards, and C# forbids a local that
/// shares a name with a local of an enclosing <em>or sibling-nested</em> local scope in the same
/// member. Both of these are legal today and <c>CS0136</c> after a naive rewrite:
/// <code>
/// var x = 1;              foreach (var item in xs) { }
/// using (…) { var x = 2; }   using (…) { var item = 2; }
/// </code>
/// The first is caught by asking what is in scope at the statement; the second is not in scope
/// anywhere and is caught only by scanning the whole member. So the rule scans the whole member.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsingDeclarationAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.UsingDeclaration);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UsingDeclaration);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.UsingStatement);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (UsingStatementSyntax)context.Node;

        // `using (stream)` over an existing variable has no declaration to turn into one.
        if (statement.Declaration is not { Variables.Count: > 0 } declaration) {
            return;
        }

        // ⚠ `using (…) using (…) { }` is one statement whose body is another `using` statement.
        // The inner one is reported on its own; rewriting the outer first would leave a half-fix
        // reported at the outer span.
        if (statement.Statement is not BlockSyntax block) {
            return;
        }

        if (statement.Parent is not BlockSyntax parent
            || !ReferenceEquals(parent.Statements[parent.Statements.Count - 1], statement)) {
            return;
        }

        // ⚠ The fix deletes two braces. An `#if` that opens inside the block and closes outside it
        // — or the reverse — survives that deletion as a file which parses under one symbol set and
        // not the other, which is the one failure mode a safe fix may never have.
        if (HasDirective(statement)
            || RewriteGuards.ContainsCommentOrDirective(
                statement.SyntaxTree,
                TextSpan.FromBounds(statement.SpanStart, declaration.SpanStart)
            )) {
            return;
        }

        if (Collides(statement, block, declaration)) {
            return;
        }

        var fix = FixEdits.Pack(
            (statement.OpenParenToken.Span, string.Empty),
            (TextSpan.FromBounds(statement.CloseParenToken.SpanStart, block.OpenBraceToken.Span.End), ";"),
            (TextSpan.FromBounds(block.CloseBraceToken.FullSpan.Start, block.CloseBraceToken.Span.End), string.Empty)
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(
                    statement.SyntaxTree,
                    TextSpan.FromBounds(statement.SpanStart, statement.CloseParenToken.Span.End)
                ),
                fix,
                "The block runs to the end of the scope, so this is a `using` declaration: `"
                + (statement.AwaitKeyword.IsKind(SyntaxKind.None) ? string.Empty : "await ")
                + "using "
                + RewriteGuards.Trim(declaration.ToString())
                + ";`"
            )
        );
    }

    /// <summary>
    /// Whether any name that changes scope is used by another local scope of the same member.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately conservative in one direction only. The set that <em>moves</em> is computed
    /// precisely — the block's own declaration space, not the scopes nested inside it, because a
    /// name two scopes deep stays a cousin of everything it was a cousin of. The set it is checked
    /// <em>against</em> is every name declared anywhere else in the member, which over-bails: a
    /// name in a scope that could never conflict still stops the rule. That asymmetry is the right
    /// one — the cost is a finding not reported, and the alternative is a fix that does not compile.
    /// </remarks>
    static bool Collides(UsingStatementSyntax statement, BlockSyntax block, VariableDeclarationSyntax declaration) {
        var moving = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var declarator in declaration.Variables) {
            moving.Add(declarator.Identifier.ValueText);
        }

        foreach (var name in OwnDeclarationSpace(block)) {
            moving.Add(name);
        }

        if (moving.Count == 0) {
            return false;
        }

        foreach (var name in moving) {
            if (RewriteGuards.DeclaredElsewhereInMember(statement, name)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The names the block itself owns — the ones the rewrite moves into the enclosing block.
    /// </summary>
    /// <remarks>
    /// ⚠ An <c>out var</c> or a declaration pattern in a top-level statement's condition is in the
    /// <em>block's</em> declaration space, not the embedded statement's, so
    /// <c>if (d.TryGetValue(k, out var v)) { }</c> contributes <c>v</c> even though no
    /// <c>LocalDeclarationStatement</c> is involved. Nested blocks are not descended into: what
    /// they declare stays nested and cannot start conflicting with anything.
    /// </remarks>
    static IEnumerable<string> OwnDeclarationSpace(BlockSyntax block) {
        foreach (var statement in block.Statements) {
            switch (statement) {
                case LocalDeclarationStatementSyntax local:
                    foreach (var declarator in local.Declaration.Variables) {
                        yield return declarator.Identifier.ValueText;
                    }

                    break;

                case LocalFunctionStatementSyntax function:
                    yield return function.Identifier.ValueText;
                    break;
            }

            // ⚠ Over-collects on purpose: a `for` or `foreach` variable is scoped to its own
            // statement and does not actually move, but counting it costs a finding and missing a
            // designation costs a broken build.
            foreach (var node in statement.DescendantNodes(static child => child is not BlockSyntax)) {
                if (node is SingleVariableDesignationSyntax designation) {
                    yield return designation.Identifier.ValueText;
                }

                if (node is ForEachStatementSyntax forEach) {
                    yield return forEach.Identifier.ValueText;
                }
            }
        }
    }

    static bool HasDirective(SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia(descendIntoTrivia: true)) {
            if (trivia.IsDirective) {
                return true;
            }
        }

        return false;
    }
}
