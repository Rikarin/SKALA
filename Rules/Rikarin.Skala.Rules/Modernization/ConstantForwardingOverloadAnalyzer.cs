using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1110</c> — a non-public overload whose whole body forwards to a longer one with a
///     constant is an optional parameter written as a second method.
/// </summary>
/// <remarks>
///     ⚠ <b>Restricted to what is not externally visible, and the restriction is the rule.</b> An
///     optional parameter's default is compiled into every call site, so changing it later does not
///     reach callers already built; and deleting an overload from a published surface is a binary
///     break outright. <c>RedundantOverload.Global</c> is the half of the ReSharper pair that carries
///     that hazard and issue #112 asks for <c>.Local</c> to ship alone. Effective accessibility is
///     computed by walking the containing types, so a <c>public</c> method on an <c>internal</c>
///     class is internal and is reported, while anything reachable from outside the assembly is not.
///     <para>
///         ⚠ <b>Exactly two methods of that name, or nothing.</b> Deleting one overload changes which
///         candidate wins for every call that used it, and with a third in the set the new winner need
///         not be the one the body forwarded to. Requiring the pair makes the outcome decidable by
///         reading the two declarations rather than by re-running overload resolution.
///     </para>
///     <para>
///         ⚠ <b>The forwarded argument is asked of the semantic model, not matched on syntax</b>, because
///         only a compile-time constant can <em>become</em> a default. <c>Render(text, text.Length)</c>
///         is a different method rather than a defaulted one.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantForwardingOverloadAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConstantForwardingOverload);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.Parent is not TypeDeclarationSyntax type
            || !IsPlainCandidate(declaration)
            || declaration.ContainsDirectives) {
            return;
        }

        // ⚠ Leading trivia may hold this method's own documentation comment — deleted with it, which
        // is intended — and nothing else. A `//` note between the two methods would be orphaned.
        foreach (var trivia in declaration.GetLeadingTrivia()) {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                || trivia.IsKind(SyntaxKind.EndOfLineTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) {
                continue;
            }

            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(declaration.SyntaxTree, declaration.Span)) {
            return;
        }

        if (Forwarded(declaration) is not { } call) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetDeclaredSymbol(declaration, cancellation) is not { } source
            || IsExternallyVisible(source)
            || source.ExplicitInterfaceImplementations.Length > 0
            || ImplementsAnInterfaceMember(source)) {
            return;
        }

        if (model.GetSymbolInfo(call, cancellation).Symbol is not IMethodSymbol target
            || SymbolEqualityComparer.Default.Equals(target, source)
            || !SymbolEqualityComparer.Default.Equals(target.ContainingType, source.ContainingType)
            || target.DeclaringSyntaxReferences.Length != 1
            || target.DeclaringSyntaxReferences[0].GetSyntax(cancellation) is not MethodDeclarationSyntax targetSyntax
            || !IsPlainCandidate(targetSyntax)
            || IsExternallyVisible(target)) {
            return;
        }

        // ⚠ The pair, and only the pair. A third overload of the same name means the call that used
        // the deleted one may now bind somewhere else entirely.
        var named = 0;
        foreach (var member in source.ContainingType.GetMembers(source.Name)) {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary }) {
                named++;
            }
        }

        if (named != 2) {
            return;
        }

        // One extra parameter, at the end, not already optional and not `params`.
        if (target.Parameters.Length != source.Parameters.Length + 1
            || !SymbolEqualityComparer.Default.Equals(target.ReturnType, source.ReturnType)) {
            return;
        }

        var extra = target.Parameters[target.Parameters.Length - 1];
        if (extra.IsOptional || extra.IsParams || extra.RefKind != RefKind.None) {
            return;
        }

        var arguments = call.ArgumentList.Arguments;
        if (arguments.Count != target.Parameters.Length) {
            return;
        }

        // Every parameter passed straight through, positionally, in order, name for name.
        for (var i = 0; i < source.Parameters.Length; i++) {
            var argument = arguments[i];
            if (argument.NameColon is not null
                || argument.RefKindKeyword.RawKind != (int)SyntaxKind.None
                || argument.Expression is not IdentifierNameSyntax identifier
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellation).Symbol,
                    source.Parameters[i]
                )
                || source.Parameters[i].RefKind != RefKind.None
                || !SymbolEqualityComparer.Default.Equals(source.Parameters[i].Type, target.Parameters[i].Type)) {
                return;
            }
        }

        var last = arguments[arguments.Count - 1];
        if (last.NameColon is not null || last.RefKindKeyword.RawKind != (int)SyntaxKind.None) {
            return;
        }

        // ⚠ Only a compile-time constant can become a default. Asked of the model, so a `const`
        // field and an enum member count and `text.Length` does not.
        var constant = model.GetConstantValue(last.Expression, cancellation);
        if (!constant.HasValue) {
            return;
        }

        var defaultText = last.Expression.ToString();
        var targetParameterSyntax =
            targetSyntax.ParameterList.Parameters[targetSyntax.ParameterList.Parameters.Count - 1];
        if (targetParameterSyntax.Default is not null) {
            return;
        }

        var fix = FixEdits.Pack(
            (TextSpan.FromBounds(declaration.FullSpan.Start, declaration.FullSpan.End), string.Empty),
            (
                TextSpan.FromBounds(targetParameterSyntax.Span.End, targetParameterSyntax.Span.End),
                " = " + defaultText
            )
        );

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                fix,
                "`"
                + source.Name
                + "` forwards to the longer overload with `"
                + RewriteGuards.Trim(defaultText)
                + "`; give `"
                + extra.Name
                + "` that default instead"
            )
        );
    }

    /// <summary>
    ///     The single call a forwarding body consists of — <c>=&gt; M(…)</c> or a lone
    ///     <c>return M(…);</c> — or <see langword="null" /> when the body does anything else.
    /// </summary>
    /// <remarks>
    ///     ⚠ Anything besides the one call is work that would be deleted with the method: a log line,
    ///     a guard, a null check. A `void` forwarder is admitted through the expression-statement
    ///     form for the same reason the returning one is.
    /// </remarks>
    static InvocationExpressionSyntax? Forwarded(MethodDeclarationSyntax method) {
        if (method.ExpressionBody is { Expression: InvocationExpressionSyntax arrow }) {
            return Named(arrow, method);
        }

        if (method.Body is not { Statements.Count: 1 } body) {
            return null;
        }

        return body.Statements[0] switch {
            ReturnStatementSyntax { Expression: InvocationExpressionSyntax call } => Named(call, method),
            ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } => Named(call, method),
            _ => null
        };
    }

    /// <summary>The call must name the same method, unqualified or through <c>this</c>.</summary>
    static InvocationExpressionSyntax? Named(InvocationExpressionSyntax call, MethodDeclarationSyntax method) {
        var name = call.Expression switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax {
                Expression: ThisExpressionSyntax,
                Name: IdentifierNameSyntax member
            } => member.Identifier.ValueText,
            _ => null
        };

        return string.Equals(name, method.Identifier.ValueText, System.StringComparison.Ordinal) ? call : null;
    }

    /// <summary>
    ///     ⚠ Any attribute is taken as intent, and so is anything that makes the declaration part of a
    ///     contract rather than a convenience.
    /// </summary>
    static bool IsPlainCandidate(MethodDeclarationSyntax method) {
        if (method.AttributeLists.Count > 0 || method.TypeParameterList is not null) {
            return false;
        }

        foreach (var modifier in method.Modifiers) {
            if (modifier.IsKind(SyntaxKind.VirtualKeyword)
                || modifier.IsKind(SyntaxKind.OverrideKeyword)
                || modifier.IsKind(SyntaxKind.AbstractKeyword)
                || modifier.IsKind(SyntaxKind.PartialKeyword)
                || modifier.IsKind(SyntaxKind.ExternKeyword)
                || modifier.IsKind(SyntaxKind.NewKeyword)) {
                return false;
            }
        }

        foreach (var parameter in method.ParameterList.Parameters) {
            if (parameter.AttributeLists.Count > 0) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Effective accessibility: the least visible link in the chain from the method to the
    ///     compilation, which is what decides whether deleting it can break somebody else's build.
    /// </summary>
    static bool IsExternallyVisible(IMethodSymbol method) {
        if (!Visible(method.DeclaredAccessibility)) {
            return false;
        }

        for (var type = method.ContainingType; type is not null; type = type.ContainingType) {
            if (!Visible(type.DeclaredAccessibility)) {
                return false;
            }
        }

        return true;
    }

    static bool Visible(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    /// <summary>Whether the method implements an interface member implicitly.</summary>
    /// <remarks>
    ///     ⚠ The overload may be the shape the interface asks for, in which case deleting it stops the
    ///     type implementing the interface — an error the fix would introduce rather than a review
    ///     note.
    /// </remarks>
    static bool ImplementsAnInterfaceMember(IMethodSymbol method) {
        var containing = method.ContainingType;
        foreach (var implemented in containing.AllInterfaces) {
            foreach (var member in implemented.GetMembers(method.Name)) {
                if (member is IMethodSymbol interfaceMethod
                    && SymbolEqualityComparer.Default.Equals(
                        containing.FindImplementationForInterfaceMember(interfaceMethod),
                        method
                    )) {
                    return true;
                }
            }
        }

        return false;
    }
}
