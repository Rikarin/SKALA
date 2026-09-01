using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2017</c> — an <c>ArgumentException</c>-family <c>paramName</c> naming no parameter in scope.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". <c>paramName</c> is an ordinary string,
///     so <c>throw new ArgumentNullException("vlaue")</c> compiles and produces a message, a
///     <c>ParamName</c> and a log line all naming a parameter the method does not have. <c>nameof</c> is
///     the same string with the compiler checking it, and it cannot go stale under a rename.
///     <para>
///         ⚠ The argument is found through the *constructor symbol's* parameter named <c>paramName</c>,
///         never by position. The family overloads the same slot: <c>ArgumentException(message,
///         paramName)</c> puts it second, <c>ArgumentNullException(paramName, message)</c> first, and
///         <c>ArgumentNullException(message, innerException)</c> has no <c>paramName</c> at all. Counting
///         arguments would report the message of that last one on every call.
///     </para>
///     <para>
///         ⚠ The rule reports only where it can name the replacement — exactly one parameter in scope a
///         single edit or a case away from the literal. A literal resembling nothing in scope is a real
///         defect and a deliberate false negative: there is no parameter the rule could put in the
///         <c>nameof</c>, and a finding whose repair <c>skala fix</c> cannot write is the shape doc 08
///         keeps out of the catalogue. rules.json § falsePositives says so where a reader will look.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WrongArgumentNameAnalyzer : DiagnosticAnalyzer {
    /// <summary>The BCL's own name for the slot. Every family constructor that carries one uses it.</summary>
    const string ParamName = "paramName";

    /// <summary>
    ///     ⚠ Below this length one edit is most of the name, so `i` and `j` are one apart and every
    ///     short parameter is a "near miss" of every other.
    /// </summary>
    const int ShortestComparableName = 3;

    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.WrongArgumentNameInException);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is not { Arguments.Count: > 0 } arguments) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !IsArgumentException(constructor.ContainingType, context.Compilation)) {
            return;
        }

        // Only a plain string literal. `nameof`, a constant, an interpolation and a variable are
        // either already right or unreadable from here, and an empty literal is a deliberate "no
        // parameter".
        if (ArgumentFor(constructor, arguments) is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression)
            || literal.Token.ValueText is not { Length: >= ShortestComparableName } written) {
            return;
        }

        var scope = ParametersInScope(creation);
        if (scope.Count == 0 || scope.Contains(written)) {
            return;
        }

        if (NearestTo(written, scope) is not { } intended) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                literal.GetLocation(),
                FixEdits.Pack((literal.Span, "nameof(" + intended + ")")),
                "`\"" + written + "\"` names no parameter in scope; `nameof(" + intended + ")` does"
            )
        );
    }

    static bool IsArgumentException(INamedTypeSymbol? type, Compilation compilation) {
        var family = compilation.GetTypeByMetadataName("System.ArgumentException");
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, family)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The expression bound to the constructor's <c>paramName</c> parameter, or null.</summary>
    /// <remarks>
    ///     ⚠ Resolved through the symbol, so a named argument lands in the right slot and an overload
    ///     without a <c>paramName</c> parameter yields nothing. Mixed named-and-positional argument
    ///     lists — legal since C# 7.2, and the one shape where a positional count is not the ordinal —
    ///     are abandoned rather than guessed at.
    /// </remarks>
    static ExpressionSyntax? ArgumentFor(IMethodSymbol constructor, BaseArgumentListSyntax arguments) {
        var ordinal = -1;
        for (var i = 0; i < constructor.Parameters.Length; i++) {
            if (string.Equals(constructor.Parameters[i].Name, ParamName, StringComparison.Ordinal)) {
                ordinal = i;
                break;
            }
        }

        if (ordinal < 0) {
            return null;
        }

        var named = false;
        foreach (var argument in arguments.Arguments) {
            if (argument.NameColon is not { } name) {
                continue;
            }

            if (string.Equals(name.Name.Identifier.ValueText, ParamName, StringComparison.Ordinal)) {
                return argument.Expression;
            }

            named = true;
        }

        return named || ordinal >= arguments.Arguments.Count ? null : arguments.Arguments[ordinal].Expression;
    }

    /// <summary>
    ///     Every parameter name the C# scoping rules make nameable at this node.
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately wider than "the enclosing method". A local function or a lambda validating the
    ///     argument of the method that encloses it names that method's parameter, and it is right to;
    ///     reporting there would be the rule's largest false-positive class. The walk therefore keeps
    ///     going outwards, collecting each enclosing function, an indexer's parameters, the implicit
    ///     <c>value</c> of a <c>set</c>, <c>init</c>, <c>add</c> or <c>remove</c> accessor, and the
    ///     containing type's primary-constructor parameters.
    ///     <para>
    ///         ⚠ It stops at a <c>static</c> lambda or <c>static</c> local function, which may not name
    ///         its enclosing parameters, and at the containing type, because an outer type's primary
    ///         constructor is not in scope inside a nested one. Stopping keeps the fix compilable, which
    ///         is the only reason the boundary matters here.
    ///     </para>
    /// </remarks>
    static HashSet<string> ParametersInScope(SyntaxNode node) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case SimpleLambdaExpressionSyntax lambda:
                    names.Add(lambda.Parameter.Identifier.ValueText);
                    if (IsStatic(lambda.Modifiers)) {
                        return names;
                    }

                    break;

                case ParenthesizedLambdaExpressionSyntax lambda:
                    Add(names, lambda.ParameterList);
                    if (IsStatic(lambda.Modifiers)) {
                        return names;
                    }

                    break;

                case AnonymousMethodExpressionSyntax anonymous:
                    Add(names, anonymous.ParameterList);
                    if (IsStatic(anonymous.Modifiers)) {
                        return names;
                    }

                    break;

                case LocalFunctionStatementSyntax local:
                    Add(names, local.ParameterList);
                    if (IsStatic(local.Modifiers)) {
                        return names;
                    }

                    break;

                case BaseMethodDeclarationSyntax method:
                    Add(names, method.ParameterList);
                    break;

                case IndexerDeclarationSyntax indexer:
                    Add(names, indexer.ParameterList);
                    break;

                case AccessorDeclarationSyntax accessor when accessor.IsKind(SyntaxKind.SetAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.InitAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.AddAccessorDeclaration)
                    || accessor.IsKind(SyntaxKind.RemoveAccessorDeclaration):
                    names.Add("value");
                    break;

                case TypeDeclarationSyntax type:
                    Add(names, type.ParameterList);
                    return names;
            }
        }

        return names;
    }

    static bool IsStatic(SyntaxTokenList modifiers) {
        foreach (var modifier in modifiers) {
            if (modifier.IsKind(SyntaxKind.StaticKeyword)) {
                return true;
            }
        }

        return false;
    }

    static void Add(HashSet<string> names, BaseParameterListSyntax? parameters) {
        if (parameters is null) {
            return;
        }

        foreach (var parameter in parameters.Parameters) {
            if (parameter.Identifier.ValueText is { Length: > 0 } name) {
                names.Add(name);
            }
        }
    }

    /// <summary>
    ///     The one parameter in scope the literal was plainly meant to be, or null when there is not
    ///     exactly one.
    /// </summary>
    /// <remarks>
    ///     ⚠ "Plainly" is a case-only difference or one edit — an insertion, a deletion, a substitution
    ///     or a transposition of two adjacent characters, which is what a typed name gets wrong. Two
    ///     candidates equally close means the rule does not know, and no candidate means the literal is
    ///     a name from somewhere else entirely; both are silent, because the message this rule writes
    ///     ends in a <c>nameof</c> it has to be able to fill in.
    /// </remarks>
    static string? NearestTo(string written, HashSet<string> scope) {
        string? found = null;
        foreach (var candidate in scope) {
            if (candidate.Length < ShortestComparableName || !IsOneEditApart(written, candidate)) {
                continue;
            }

            if (found is not null) {
                return null;
            }

            found = candidate;
        }

        return found;
    }

    static bool IsOneEditApart(string left, string right) {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (Math.Abs(left.Length - right.Length) > 1) {
            return false;
        }

        // The common prefix and suffix cancel; what is left in the middle is the edit.
        var start = 0;
        while (start < left.Length && start < right.Length && left[start] == right[start]) {
            start++;
        }

        var end = 0;
        while (end < left.Length - start
            && end < right.Length - start
            && left[left.Length - 1 - end] == right[right.Length - 1 - end]) {
            end++;
        }

        var remainingLeft = left.Length - start - end;
        var remainingRight = right.Length - start - end;

        // One insertion or deletion leaves a single character on one side and nothing on the other;
        // one substitution leaves one on each; one adjacent transposition leaves the same two, swapped.
        if (remainingLeft <= 1 && remainingRight <= 1) {
            return true;
        }

        return remainingLeft == 2
            && remainingRight == 2
            && left[start] == right[start + 1]
            && left[start + 1] == right[start];
    }
}
