using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3020</c> — a non-<c>async</c> method whose return type is <c>Task</c> returns null.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". A caller writes
///     <c>await Something()</c>, which is what a <c>Task</c> return type invites, and gets a
///     <c>NullReferenceException</c> whose stack names the caller rather than the method that returned
///     null. An <c>async</c> method cannot do this — the compiler wraps its result — so the shape only
///     exists where somebody wrote the signature by hand, and there it looks exactly like every other
///     "return nothing" in the file.
///     <para>
///         ⚠ The fix is <c>Task.CompletedTask</c> for the non-generic form, which is unambiguous, and
///         <c>Task.FromResult&lt;T&gt;(default!)</c> for the generic one, which is not: the *value* a
///         completed task should carry is not derivable from the method. That is why the fix is
///         declared unsafe rather than safe, and why the replacement is written down here rather than
///         inferred.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullTaskReturnAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullReturnedFromTaskMethod);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var task = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
                var generic = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
                if (task is null && generic is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, task, generic),
                    SyntaxKind.ReturnStatement,
                    SyntaxKind.ArrowExpressionClause
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol? task, INamedTypeSymbol? generic) {
        var (expression, function) = context.Node switch {
            ReturnStatementSyntax { Expression: { } value } statement => (value, EnclosingFunction(statement)),
            ArrowExpressionClauseSyntax arrow => (arrow.Expression, arrow.Parent),
            _ => (null, null)
        };

        // ⚠ Syntax first: `null` or `default` is what makes this shape, and almost no `return` is
        // either, so the question that costs nothing is asked before any symbol is looked up.
        if (expression is null
            || !expression.IsKind(SyntaxKind.NullLiteralExpression)
            && !expression.IsKind(SyntaxKind.DefaultLiteralExpression)) {
            return;
        }

        SyntaxTokenList modifiers;
        TypeSyntax returnType;
        switch (function) {
            case MethodDeclarationSyntax method:
                (modifiers, returnType) = (method.Modifiers, method.ReturnType);
                break;
            case LocalFunctionStatementSyntax local:
                (modifiers, returnType) = (local.Modifiers, local.ReturnType);
                break;
            default:
                return;
        }

        // ⚠ An `async Task<string>` method's `return null;` returns a null *string* inside a real
        // task, which is an ordinary nullable-reference question and not this rule's business.
        if (modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.AsyncKeyword))) {
            return;
        }

        // ⚠ `Task?` is the author saying null is a value this method returns. The rule disagrees
        // with the design and has nothing to report: the contract already carries the warning that
        // an unguarded `await` would be wrong.
        if (returnType is NullableTypeSyntax) {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(returnType, context.CancellationToken).Type
            is not INamedTypeSymbol declared) {
            return;
        }

        var definition = declared.OriginalDefinition;
        var isGeneric = generic is not null && SymbolEqualityComparer.Default.Equals(definition, generic);
        if (!isGeneric && (task is null || !SymbolEqualityComparer.Default.Equals(definition, task))) {
            return;
        }

        var replacement = Replacement(context, returnType.SpanStart, declared, isGeneric);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                expression.GetLocation(),
                FixEdits.Pack((expression.Span, replacement)),
                "this method is not `async`, so `await` on what it returns is a `NullReferenceException`"
            )
        );
    }

    /// <summary>
    ///     The completed task to return instead, spelled so that it binds where it is written.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not derived from the return type's syntax. <c>using MyTask = System.Threading.Tasks.Task;</c>
    ///     makes the written name an alias, a nested or aliased spelling makes it something else again,
    ///     and a fix built by string-editing the declaration would produce a name that does not resolve.
    ///     What is asked instead is whether the simple name <c>Task</c> means
    ///     <c>System.Threading.Tasks.Task</c> at this position; where it does not, the fully qualified
    ///     name is used, which always binds.
    /// </remarks>
    static string Replacement(
        SyntaxNodeAnalysisContext context,
        int position,
        INamedTypeSymbol declared,
        bool isGeneric
    ) {
        const string FullName = "global::System.Threading.Tasks.Task";
        var name = context.SemanticModel.LookupNamespacesAndTypes(position, name: "Task")
            .Any(static symbol => string.Equals(
                    symbol.ToDisplayString(),
                    "System.Threading.Tasks.Task",
                    System.StringComparison.Ordinal
                )
            )
                ? "Task"
                : FullName;

        if (!isGeneric) {
            return name + ".CompletedTask";
        }

        var argument = declared.TypeArguments[0]
            .ToMinimalDisplayString(context.SemanticModel, position);

        // ⚠ `default` and not a guess at a better value. What a completed task should carry is a
        // decision about the method, which is exactly why `fixIsSafe` is false.
        return name + ".FromResult<" + argument + ">(default!)";
    }

    static SyntaxNode? EnclosingFunction(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                    return current;

                // A `return` inside any of these belongs to it, not to the method around it.
                case AnonymousFunctionExpressionSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }
}
