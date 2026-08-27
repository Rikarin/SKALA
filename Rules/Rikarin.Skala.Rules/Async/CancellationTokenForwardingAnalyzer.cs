using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
/// <c>SK3004</c> — the method took a <c>CancellationToken</c> and did not pass it on.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". A token that stops at
/// the first frame is a cancellation that does not happen: the caller sees the parameter accepted,
/// assumes the operation is cancellable, and gets work that runs to completion after whoever asked
/// for it has gone.
/// <para>
/// ⚠ Two shapes, and no third. Either the callee declares an <em>optional</em>
/// <c>CancellationToken</c> this call omitted — repaired with a named argument, which is right
/// whether or not the parameters in between were supplied — or an overload exists whose parameter
/// list is this one with a <c>CancellationToken</c> appended, and every argument here is positional
/// — repaired by appending. Anywhere the rule would have to choose between overloads it says
/// nothing, because a fix that changes which method is called is not a fix.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenForwardingAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CancellationTokenNotForwarded);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var token = start.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
                if (token is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, token), SyntaxKind.InvocationExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol tokenType) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // ⚠ Cleanup that a cancellation can abort is worse than cleanup that ignores one, and a
        // `catch` or a `finally` is exactly where that cleanup lives.
        if (IsInsideCleanup(invocation) || AsyncContext.IsTestMethod(invocation)) {
            return;
        }

        var available = TokenInScope(context, invocation, tokenType);
        if (available is null) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ReducedExtension } target) {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (Supplies(arguments, target, tokenType)) {
            return;
        }

        var edit = Omitted(target, tokenType) is { } optional
            ? Append(invocation, arguments, optional.Name + ": " + available)
            : HasAppendedOverload(target, tokenType) && AllPositional(arguments, target)
                ? Append(invocation, arguments, available)
                : (Edit?)null;

        if (edit is null) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((edit.Value.Span, edit.Value.Text)),
                "`" + target.Name + "` takes a `CancellationToken` and `" + available + "` is not passed to it"
            )
        );
    }

    readonly record struct Edit(TextSpan Span, string Text);

    static Edit? Append(InvocationExpressionSyntax invocation, SeparatedSyntaxList<ArgumentSyntax> arguments, string text) {
        var list = invocation.ArgumentList;
        return arguments.Count == 0
            ? new Edit(new TextSpan(list.CloseParenToken.SpanStart, 0), text)
            : new Edit(new TextSpan(arguments[arguments.Count - 1].Span.End, 0), ", " + text);
    }

    /// <summary>
    /// The one <c>CancellationToken</c> parameter in scope, or null when there is none or several.
    /// </summary>
    /// <remarks>
    /// ⚠ Several is not a harder case; it is a different one. Which token an inner call should get
    /// when two are in scope is a decision about intent, and a rule that picks is a rule that is
    /// sometimes silently wrong rather than sometimes silent.
    /// </remarks>
    static string? TokenInScope(SyntaxNodeAnalysisContext context, SyntaxNode node, INamedTypeSymbol tokenType) {
        string? found = null;
        for (var current = node.Parent; current is not null; current = current.Parent) {
            var parameters = current switch {
                MethodDeclarationSyntax method => method.ParameterList.Parameters,
                LocalFunctionStatementSyntax local => local.ParameterList.Parameters,
                ParenthesizedLambdaExpressionSyntax lambda => lambda.ParameterList.Parameters,
                _ => default
            };

            foreach (var parameter in parameters) {
                // ⚠ The name in the source before the symbol behind it, and it is worth 300 ms of
                // the 330 this rule cost before the order was measured (docs/plan/13 § "Analysis").
                // This runs on every invocation in the tree, and where most methods take no token,
                // resolving every parameter to ask is the whole cost of the rule.
                //
                // ⚠ The symbol still decides; the text only decides whether to ask. What it costs
                // is a method whose token parameter is written through a `using` alias, which the
                // filter reads as some other type and the rule then never sees. That is a missed
                // finding rather than a wrong one, and it is the direction this rule errs in
                // everywhere else too.
                if (!NamesCancellationToken(parameter.Type)
                    || context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol
                    || !SymbolEqualityComparer.Default.Equals(symbol.Type, tokenType)) {
                    continue;
                }

                if (found is not null) {
                    return null;
                }

                found = parameter.Identifier.ValueText;
            }

            if (current is MemberDeclarationSyntax) {
                break;
            }
        }

        return found;
    }

    /// <summary>Whether the written type could be <c>CancellationToken</c>, by its last name.</summary>
    static bool NamesCancellationToken(TypeSyntax? type) =>
        type switch {
            IdentifierNameSyntax name => string.Equals(
                name.Identifier.ValueText,
                "CancellationToken",
                StringComparison.Ordinal
            ),
            QualifiedNameSyntax qualified => NamesCancellationToken(qualified.Right),
            AliasQualifiedNameSyntax aliased => NamesCancellationToken(aliased.Name),
            _ => false
        };

    /// <summary>An optional <c>CancellationToken</c> parameter, which a named argument can fill.</summary>
    static IParameterSymbol? Omitted(IMethodSymbol target, INamedTypeSymbol tokenType) {
        foreach (var parameter in target.Parameters) {
            if (parameter.IsOptional && SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType)) {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a sibling overload is this method's parameter list with a token appended.
    /// </summary>
    /// <remarks>
    /// ⚠ Appended, and nothing else changed. That is what makes appending an argument select it:
    /// any other difference and the rule would be guessing at overload resolution, which is how a
    /// fix comes to call a different method than the one it was reported against.
    /// </remarks>
    static bool HasAppendedOverload(IMethodSymbol target, INamedTypeSymbol tokenType) {
        foreach (var member in target.ContainingType.GetMembers(target.Name)) {
            if (member is not IMethodSymbol candidate
                || SymbolEqualityComparer.Default.Equals(candidate, target)
                || candidate.Parameters.Length != target.Parameters.Length + 1
                || candidate.TypeParameters.Length != target.TypeParameters.Length
                || candidate.IsStatic != target.IsStatic
                || !SymbolEqualityComparer.Default.Equals(
                    candidate.Parameters[candidate.Parameters.Length - 1].Type,
                    tokenType
                )) {
                continue;
            }

            var matches = true;
            for (var i = 0; i < target.Parameters.Length; i++) {
                if (target.Parameters[i].RefKind != candidate.Parameters[i].RefKind
                    || !SymbolEqualityComparer.Default.Equals(
                        target.Parameters[i].Type,
                        candidate.Parameters[i].Type
                    )) {
                    matches = false;
                    break;
                }
            }

            if (matches) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the call already hands the callee a token, positionally or by name.
    /// </summary>
    /// <remarks>
    /// ⚠ Includes <c>CancellationToken.None</c> and <c>default</c>. Writing the token out is how an
    /// author says a call is deliberately not cancellable; a rule that overrides that is arguing
    /// with a decision rather than finding an omission.
    /// </remarks>
    static bool Supplies(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol target,
        INamedTypeSymbol tokenType
    ) {
        for (var i = 0; i < arguments.Count; i++) {
            var argument = arguments[i];
            if (argument.NameColon is { } name) {
                foreach (var parameter in target.Parameters) {
                    if (string.Equals(parameter.Name, name.Name.Identifier.ValueText, StringComparison.Ordinal)
                        && SymbolEqualityComparer.Default.Equals(parameter.Type, tokenType)) {
                        return true;
                    }
                }

                continue;
            }

            if (i < target.Parameters.Length
                && SymbolEqualityComparer.Default.Equals(target.Parameters[i].Type, tokenType)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether every parameter is filled positionally, so appending one more selects the overload.
    /// </summary>
    static bool AllPositional(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol target) {
        if (arguments.Count != target.Parameters.Length) {
            return false;
        }

        foreach (var argument in arguments) {
            if (argument.NameColon is not null || argument.RefKindKeyword.RawKind != (int)SyntaxKind.None) {
                return false;
            }
        }

        foreach (var parameter in target.Parameters) {
            if (parameter.IsParams || parameter.RefKind != RefKind.None) {
                return false;
            }
        }

        return true;
    }

    static bool IsInsideCleanup(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case CatchClauseSyntax:
                case FinallyClauseSyntax:
                    return true;
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
