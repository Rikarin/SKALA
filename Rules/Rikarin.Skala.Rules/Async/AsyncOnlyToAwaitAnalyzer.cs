using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3031</c> — the whole method is <c>return await X()</c>, so the state machine does nothing.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". An <c>async</c> method
///     compiles to a state machine, an allocation and a resumption path. Where the body is one
///     <c>await</c> whose result is returned unchanged, all of that exists to hand back a task the
///     method already had.
///     <para>
///         ⚠ <b>Not a safe fix, and the reason is not the edit.</b> Eliding the <c>await</c> moves
///         exceptions from the returned task to the call — a method that used to fault its task now
///         throws synchronously — and drops the method from every stack trace the task carries. Both are
///         behaviour, both are usually fine, and neither is something a tool decides.
///     </para>
///     <para>
///         ⚠
///         <b>
///             It is outright wrong inside a <c>using</c>, which is what <c>SK3007</c> reports — and the
///             two are disjoint by construction rather than by <c>supersedes</c>.
///         </b> The body here must be
///         a <em>single</em> statement that is the <c>return await</c>, or a single expression body. A
///         <c>using</c> declaration needs a statement before the return and a <c>using</c> statement
///         makes the block's one statement a <c>using</c> rather than a <c>return</c>, so no shape this
///         rule reports can contain one. <c>supersedes</c> would have been the wrong instrument anyway:
///         <c>Supersession.Apply</c> suppresses the <em>superseded</em> finding, so it hides the one
///         carrying the remedy.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncOnlyToAwaitAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.AsyncOnlyToAwait);

    static readonly string[] TaskTypes = [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask",
        "System.Threading.Tasks.ValueTask`1"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var tasks = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in TaskTypes) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        tasks.Add(type);
                    }
                }

                if (tasks.Count == 0) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, tasks),
                    SyntaxKind.MethodDeclaration,
                    SyntaxKind.LocalFunctionStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, HashSet<INamedTypeSymbol> tasks) {
        SyntaxTokenList modifiers;
        BlockSyntax? block;
        ArrowExpressionClauseSyntax? arrow;
        switch (context.Node) {
            case MethodDeclarationSyntax method:
                (modifiers, block, arrow) = (method.Modifiers, method.Body, method.ExpressionBody);
                break;
            case LocalFunctionStatementSyntax local:
                (modifiers, block, arrow) = (local.Modifiers, local.Body, local.ExpressionBody);
                break;
            default:
                return;
        }

        if (!AsyncKeywordOf(modifiers, out var async)) {
            return;
        }

        // ⚠ The whole body, and there is exactly one shape of it. This is also what keeps the rule
        // out of SK3007's territory: a `using` declaration needs a statement before the return, and
        // a `using` statement makes the block's one statement a `using` rather than a `return`.
        if (!SoleAwait(block, arrow, out var awaited, out var edit)) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol declared
            || declared.ReturnType is not INamedTypeSymbol returned
            || !tasks.Contains(returned.OriginalDefinition)) {
            return;
        }

        // ⚠ `return E;` has to bind. `await GetValueTask()` inside an `async Task<int>` is legal and
        // `return GetValueTask()` is not, so the operand's type must be the declared return type
        // rather than merely awaitable to it. This is also what excludes `.ConfigureAwait(false)`,
        // whose type is a configured awaitable and not a task at all.
        if (!SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetTypeInfo(awaited.Expression, context.CancellationToken).Type,
                returned
            )) {
            return;
        }

        // ⚠ CS4032: an `await` left inside the operand would be an `await` in a method that is no
        // longer `async`. `return await Read(await Open());` is the shape, and the fix for it parses
        // and does not compile.
        foreach (var node in awaited.Expression.DescendantNodes(static child =>
                     child is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax
                 )) {
            if (node is AwaitExpressionSyntax) {
                return;
            }
        }

        if (!Delete(async, out var removal)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                async.GetLocation(),
                FixEdits.Pack(removal, edit),
                "`" + declared.Name + "` allocates a state machine to await a task it hands straight back"
            )
        );
    }

    static bool AsyncKeywordOf(SyntaxTokenList modifiers, out SyntaxToken async) {
        async = default;
        var found = false;
        foreach (var modifier in modifiers) {
            switch ((SyntaxKind)modifier.RawKind) {
                case SyntaxKind.AsyncKeyword:
                    async = modifier;
                    found = true;
                    continue;

                // `partial` splits the signature across declarations, and neither `abstract` nor
                // `extern` has a body to read.
                case SyntaxKind.PartialKeyword:
                case SyntaxKind.AbstractKeyword:
                case SyntaxKind.ExternKeyword:
                    return false;
            }
        }

        return found;
    }

    /// <summary>
    ///     The one <c>await</c> that is the whole body, and the edit that removes it.
    /// </summary>
    /// <remarks>
    ///     Three spellings, two edits. <c>return await E;</c> and <c>=&gt; await E</c> drop the keyword;
    ///     <c>await E;</c> in an <c>async Task</c> body becomes <c>return E;</c>, because CS1997
    ///     forbids the first spelling there and falling off the end is what the method did.
    /// </remarks>
    static bool SoleAwait(
        BlockSyntax? block,
        ArrowExpressionClauseSyntax? arrow,
        out AwaitExpressionSyntax awaited,
        out (TextSpan Span, string Text) edit
    ) {
        awaited = null!;
        edit = default;

        if (block is null) {
            if (arrow?.Expression is not AwaitExpressionSyntax expression) {
                return false;
            }

            awaited = expression;
            return Delete(expression.AwaitKeyword, out edit);
        }

        if (arrow is not null || block.Statements.Count != 1) {
            return false;
        }

        switch (block.Statements[0]) {
            case ReturnStatementSyntax { Expression: AwaitExpressionSyntax value }:
                awaited = value;
                return Delete(value.AwaitKeyword, out edit);

            case ExpressionStatementSyntax { Expression: AwaitExpressionSyntax value }:
                awaited = value;
                edit = (value.AwaitKeyword.Span, "return");
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Deletes a keyword and the whitespace after it.
    /// </summary>
    /// <remarks>
    ///     ⚠ Whitespace only. Trivia between the keyword and what follows it is a comment somebody
    ///     wrote, and a fix that takes it out with the keyword deletes a note nobody asked it to.
    /// </remarks>
    static bool Delete(SyntaxToken keyword, out (TextSpan Span, string Text) edit) {
        var end = keyword.Span.End;
        foreach (var trivia in keyword.TrailingTrivia) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia)) {
                edit = default;
                return false;
            }

            end = trivia.Span.End;
        }

        edit = (TextSpan.FromBounds(keyword.SpanStart, end), string.Empty);
        return true;
    }
}
