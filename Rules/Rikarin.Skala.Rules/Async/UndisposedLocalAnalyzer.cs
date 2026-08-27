using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
/// <c>SK3501</c> — a disposable is constructed into a local that never leaves the method and is
/// never disposed.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". A handle held until a
/// finalizer runs — or for ever, where there is none — shows up as a file that cannot be reopened,
/// a pool that empties or a socket stuck in <c>CLOSE_WAIT</c>, always somewhere other than the
/// method that leaked it.
/// <para>
/// ⚠ <b>Ownership is the whole difficulty and the rule refuses to guess at it.</b> Every way the
/// object might outlive the method — returned, assigned, passed, captured, yielded — withdraws the
/// finding, and so does a constructor argument that is itself disposable, because
/// <c>new StreamReader(stream)</c> takes ownership of <c>stream</c> and a <c>using</c> on the
/// reader would close a stream the caller still owns. That last guard is what makes the fix safe
/// rather than merely plausible.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UndisposedLocalAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.DisposableNotDisposed);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DisposableNotDisposed);

    /// <summary>
    /// ⚠ Disposables that nobody disposes, or that must not be disposed here.
    /// </summary>
    /// <remarks>
    /// A <c>Task</c> is <c>IDisposable</c> and the framework's own guidance is to leave it alone. A
    /// timer whose local is disposed at the end of the method that started it never fires again,
    /// so the "fix" would delete the feature.
    /// </remarks>
    static readonly string[] Excluded = [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1", "System.Threading.Timer",
        "System.Timers.Timer"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                if (disposable is null) {
                    return;
                }

                var excluded = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var name in Excluded) {
                    if (start.Compilation.GetTypeByMetadataName(name) is { } type) {
                        excluded.Add(type);
                    }
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, excluded),
                    SyntaxKind.LocalDeclarationStatement
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol disposable,
        HashSet<INamedTypeSymbol> excluded
    ) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.Declaration.Variables.Count != 1
            || statement.Parent is not BlockSyntax) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer?.Value is not BaseObjectCreationExpressionSyntax creation) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol local
            || local.Type is not INamedTypeSymbol type
            || type.TypeKind == TypeKind.Error
            || excluded.Contains(type.OriginalDefinition)
            || !Implements(type, disposable)) {
            return;
        }

        // ⚠ Ownership passed *inward*. `new StreamReader(stream)` disposes `stream` when the reader
        // is disposed, so a `using` here closes something the caller may still be holding.
        foreach (var argument in creation.ArgumentList?.Arguments ?? default) {
            var argumentType = context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).Type;
            if (argumentType is null || Implements(argumentType, disposable)) {
                return;
            }
        }

        var body = EnclosingBody(statement);
        if (body is null || IsIterator(body)) {
            return;
        }

        if (!KeepsOwnership(context, local, body, statement)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declarator.Identifier.GetLocation(),
                FixEdits.Pack((new TextSpan(statement.SpanStart, 0), "using ")),
                "`" + local.Name + "` is a `" + type.Name + "` that is never disposed"
            )
        );
    }

    /// <summary>
    /// Whether every reference to the local is one that leaves the object where it was created.
    /// </summary>
    /// <remarks>
    /// ⚠ The default is "no". A reference the rule does not recognise withdraws the finding, so a
    /// language construct nobody thought about here costs a missed finding rather than a wrong one.
    /// </remarks>
    static bool KeepsOwnership(
        SyntaxNodeAnalysisContext context,
        ILocalSymbol local,
        SyntaxNode body,
        LocalDeclarationStatementSyntax declaration
    ) {
        var read = false;
        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>()) {
            if (!string.Equals(identifier.Identifier.ValueText, local.Name, StringComparison.Ordinal)
                || declaration.Span.Contains(identifier.Span)) {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                    local
                )) {
                continue;
            }

            for (var current = identifier.Parent; current is not null && !ReferenceEquals(current, body);
                 current = current.Parent) {
                switch (current) {
                    // ⚠ A reference inside a lambda or a local function may run after the method
                    // returns, and the rule has no way to say when.
                    case AnonymousFunctionExpressionSyntax:
                    case LocalFunctionStatementSyntax:
                        return false;

                    // ⚠ A read inside a returned expression is the case that decides whether this
                    // rule is safe. `return Task.Delay(10, source.Token);` reads the source and
                    // hands the caller something that is still using it, so disposing at the end of
                    // *this* method is wrong — and the rule cannot tell that apart from
                    // `return stream.ReadByte();`, where it would be right. It withholds both.
                    case ReturnStatementSyntax:
                        return false;
                }
            }

            // ⚠ `x!` and `(x)` wrap the reference without changing what it is, and skipping them is
            // what stops `x!.Dispose()` being read as a use rather than as a disposal.
            var reference = (ExpressionSyntax)identifier;
            while (reference.Parent is ParenthesizedExpressionSyntax
                   or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }) {
                reference = (ExpressionSyntax)reference.Parent;
            }

            switch (reference.Parent) {
                // `x.Member` — reading the object without handing it anywhere.
                case MemberAccessExpressionSyntax access when ReferenceEquals(access.Expression, reference):
                    if (IsDisposal(access.Name.Identifier.ValueText)) {
                        return false;
                    }

                    read = true;
                    continue;

                // `x[i]` — the same shape through an indexer.
                case ElementAccessExpressionSyntax element when ReferenceEquals(element.Expression, reference):
                    read = true;
                    continue;

                // `x?.Member` — the member sits in a binding under `WhenNotNull`, not under `x`.
                case ConditionalAccessExpressionSyntax conditional
                    when ReferenceEquals(conditional.Expression, reference):
                    foreach (var binding in conditional.WhenNotNull.DescendantNodesAndSelf()
                                 .OfType<MemberBindingExpressionSyntax>()) {
                        if (IsDisposal(binding.Name.Identifier.ValueText)) {
                            return false;
                        }
                    }

                    read = true;
                    continue;

                default:
                    return false;
            }
        }

        return read;
    }

    static bool IsDisposal(string name) => name is "Dispose" or "DisposeAsync" or "Close";

    static SyntaxNode? EnclosingBody(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    /// <summary>
    /// ⚠ An iterator's locals live as long as the enumerator, which is not this method's scope.
    /// </summary>
    static bool IsIterator(SyntaxNode body) =>
        body.DescendantNodes(static child => child is not AnonymousFunctionExpressionSyntax
                                 and not LocalFunctionStatementSyntax)
            .Any(static node => node is YieldStatementSyntax);

    static bool Implements(ITypeSymbol type, INamedTypeSymbol disposable) {
        if (SymbolEqualityComparer.Default.Equals(type, disposable)) {
            return true;
        }

        foreach (var candidate in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(candidate, disposable)) {
                return true;
            }
        }

        return false;
    }
}
