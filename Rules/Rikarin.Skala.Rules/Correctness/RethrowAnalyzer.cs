using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
/// <c>SK2015</c> — <c>throw ex;</c> inside <c>catch (… ex)</c> is <c>throw;</c>.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". The two forms look interchangeable and
/// are not: <c>throw ex;</c> assigns the exception a fresh stack trace starting at the handler, so
/// every frame between the fault and the <c>catch</c> is erased — which is the one piece of
/// information the log existed to carry.
/// <para>
/// ⚠ Purely syntactic, and deliberately so. The question "is this identifier the one this catch
/// clause declared" is answered by the declaration in scope, which the syntax tree already knows;
/// asking the semantic model would make the rule need a project without making it more right. The
/// one thing syntax cannot see — whether some other declaration shadows the name — cannot happen,
/// because C# forbids a local that shadows the catch variable inside the same clause.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RethrowAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RethrowLosesStackTrace);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (ThrowStatementSyntax)context.Node;

        // `throw;` is already the right thing; `throw Wrap(ex);` is a different program.
        if (statement.Expression is not IdentifierNameSyntax thrown) {
            return;
        }

        var clause = EnclosingCatch(statement);
        if (clause?.Declaration is not { Identifier.ValueText.Length: > 0 } declaration) {
            return;
        }

        // ⚠ The *nearest* enclosing clause, not any of them. `throw outer;` from inside an inner
        // catch does not become `throw;`: the bare form re-throws the inner exception, which is a
        // different exception. `EnclosingCatch` stops at the first clause it meets, so a mismatch
        // here is exactly that case and the rule stays silent.
        if (!string.Equals(declaration.Identifier.ValueText, thrown.Identifier.ValueText, StringComparison.Ordinal)) {
            return;
        }

        // ⚠ A clause that reassigns its own variable is throwing something else through the same
        // name. Rare, legal, and the one shape where the two forms genuinely differ.
        if (IsAssignedIn(clause, thrown.Identifier.ValueText)) {
            return;
        }

        var fix = FixEdits.Pack((statement.Span, "throw;"));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                fix,
                "`throw " + thrown.Identifier.ValueText + ";` resets the stack trace; use `throw;`"
            )
        );
    }

    /// <summary>
    /// The catch clause this <c>throw</c> would re-throw from, or null when a bare <c>throw;</c>
    /// would not be legal here.
    /// </summary>
    /// <remarks>
    /// ⚠ The walk stops at a lambda, a local function and an accessor, because a bare <c>throw;</c>
    /// inside one of those is not a re-throw of the outer clause's exception — it is CS0156. A fix
    /// that does not compile is worse than no fix (docs/plan/10), so the finding is withheld rather
    /// than the fix alone. It also stops at a <c>finally</c> block for the same reason.
    /// </remarks>
    static CatchClauseSyntax? EnclosingCatch(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case CatchClauseSyntax clause:
                    return clause;

                case FinallyClauseSyntax:
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case AccessorDeclarationSyntax:
                case BaseMethodDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    /// <summary>Whether the clause assigns the name anywhere, including through <c>ref</c>/<c>out</c>.</summary>
    static bool IsAssignedIn(CatchClauseSyntax clause, string name) {
        foreach (var node in clause.DescendantNodes()) {
            switch (node) {
                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax left }
                    when string.Equals(left.Identifier.ValueText, name, StringComparison.Ordinal):
                    return true;

                case ArgumentSyntax {
                    Expression: IdentifierNameSyntax argument,
                    RefOrOutKeyword.RawKind: not (int)SyntaxKind.None
                } when string.Equals(argument.Identifier.ValueText, name, StringComparison.Ordinal):
                    return true;
            }
        }

        return false;
    }
}
