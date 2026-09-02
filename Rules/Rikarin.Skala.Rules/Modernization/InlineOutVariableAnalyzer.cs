using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1054</c> — a bare declaration whose only purpose is the <c>out</c> argument below it.
/// </summary>
/// <remarks>
///     The C# 7 form has been available for eight years and the separated form is still what a model
///     writes, because most of its training data predates it. <c>SK1033</c> already reports the
///     <c>TryGetValue</c> shape this most often appears in.
///     <para>
///         ⚠ <b>The declared type is carried across verbatim, never replaced by <c>var</c>.</b>
///         <c>out</c> is invariant, so the written type is exactly the parameter's and reproducing it
///         leaves overload resolution where it was; <c>out var</c> is typeless in that respect and can
///         move it. Reproducing the text rather than the symbol also keeps an alias or a
///         <c>using</c>-shortened name spelled the way the file spells it.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Scope is the whole risk, and it is answered by position rather than by rules about
///             each statement kind.
///         </b> An expression variable is scoped no wider than the statement that
///         introduces it, so the rule requires every reference to the local to be inside the statement
///         the <c>out</c> argument belongs to, and requires the argument to sit in that statement's own
///         expression — not inside a nested block, a lambda or a local function, each of which would
///         scope the new declaration somewhere the old one was not.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlineOutVariableAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.InlineOutVariable);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.InlineOutVariable);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.AttributeLists.Count > 0
            || statement.Declaration.Variables.Count != 1
            || statement.Declaration.Type.IsVar) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer is not null || Next(statement) is not { } following) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol {
                RefKind: RefKind.None,
                IsConst: false
            } local) {
            return;
        }

        // Exactly one `out` argument naming the local, and every other mention of it inside the same
        // statement. Two out-arguments would be two declarations of one name.
        ArgumentSyntax? target = null;
        foreach (var node in following.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, local.Name, System.StringComparison.Ordinal)
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellation).Symbol,
                    local
                )) {
                continue;
            }

            if (identifier.Parent is ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.OutKeyword } argument) {
                if (target is not null) {
                    return;
                }

                target = argument;
            }
        }

        if (target is null
            || !InTheStatementsOwnExpression(target, following)
            || RewriteGuards.ReferencedOutside(model, local, following, declarator, cancellation)) {
            return;
        }

        if (NullComparison.InsideExpressionTree(model, following, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(statement)
            || RewriteGuards.ContainsCommentOrDirective(statement.SyntaxTree, statement.FullSpan)) {
            return;
        }

        var replacement = "out " + statement.Declaration.Type + " " + local.Name;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declarator.Identifier.GetLocation(),
                FixEdits.Pack(
                    (target.Span, replacement),
                    (RewriteGuards.LineSpanOf(statement), string.Empty)
                ),
                "The declaration belongs in the call: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    /// <summary>The statement immediately after this one in the same block or switch section.</summary>
    static StatementSyntax? Next(StatementSyntax statement) {
        var siblings = statement.Parent switch {
            BlockSyntax block => (IReadOnlyList<StatementSyntax>)block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => null
        };

        if (siblings is null) {
            return null;
        }

        for (var i = 0; i < siblings.Count - 1; i++) {
            if (siblings[i] == statement) {
                return siblings[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    ///     ⚠ Whether the argument sits in the statement's own expression rather than inside something
    ///     the statement merely contains.
    /// </summary>
    /// <remarks>
    ///     An <c>out</c> variable declared in a nested block, a lambda or a local function is scoped
    ///     there, and the local it replaces was scoped to the enclosing block. That is a fix which
    ///     compiles in the small cases and stops compiling as soon as the name is used twice.
    /// </remarks>
    static bool InTheStatementsOwnExpression(SyntaxNode argument, StatementSyntax statement) {
        for (var current = argument.Parent; current is not null && current != statement; current = current.Parent) {
            if (current is StatementSyntax
                or BlockSyntax
                or AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax) {
                return false;
            }
        }

        return true;
    }

}
