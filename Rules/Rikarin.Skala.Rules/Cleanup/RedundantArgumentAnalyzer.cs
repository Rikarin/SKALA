using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0232</c> — an argument or a signature element that restates what the declaration already says.
/// </summary>
/// <remarks>
///     <para>
///         Four shapes: a trailing argument that repeats the parameter's default,
///         <c>new EventHandler(Foo)</c> where <c>Foo</c> would convert on its own, explicit lambda
///         parameter types under a target that already fixes them, and an anonymous method's signature
///         whose parameters nothing uses. The first is the one with teeth — an argument restating a
///         default is a value that stops tracking the default the day it changes, which is the hazard
///         <c>optional-and-params-hazards</c> reports from the declaration side.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Deleting an argument can change which method is called, so the rule asks the compiler
///             rather than reasoning about it.
///         </b> With <c>Foo(int a, int b = 0)</c> beside <c>Foo(int a)</c>,
///         <c>Foo(1, 0)</c> and <c>Foo(1)</c> are calls to different methods, and nothing about the
///         argument says so. Every finding here re-binds the shortened call speculatively and withdraws
///         unless the same symbol comes back. The other three shapes avoid the question instead of
///         answering it: each is reported only where the target type is written down.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantArgument);

    /// <summary>
    ///     ⚠ The attributes that make an omitted argument mean something other than the default.
    /// </summary>
    /// <remarks>
    ///     Passing <c>null</c> to a <c>[CallerMemberName] string name = null</c> parameter looks exactly
    ///     like restating the default and is the opposite: omit it and the compiler substitutes the
    ///     caller's name, so the "redundant" argument is the only thing keeping the value null.
    /// </remarks>
    static readonly string[] CallerInfo = [
        "CallerMemberNameAttribute", "CallerLineNumberAttribute", "CallerFilePathAttribute",
        "CallerArgumentExpressionAttribute"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeDelegateCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAnonymousMethod, SyntaxKind.AnonymousMethodExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        // Positional only. A named argument does not have to be last and does not have to fill the
        // parameter its position names, so "the trailing argument's parameter" stops being a fact.
        foreach (var argument in arguments) {
            if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
                return;
            }
        }

        // ⚠ **The bound is on the index, never on the counter.** `Take(1, 2, 3)` against
        // `Take(int first, params int[] rest)` has three arguments and two parameters, and the old
        // guard bounded `restated` while indexing at `arguments.Count - 1 - restated` — two numbers
        // that agree only when the counts do. `method.Parameters[2]` on a two-element array threw
        // `IndexOutOfRangeException` on *every* expanded `params` call, which is every Serilog and
        // every `string.Format` call, and an analyzer exception is `AD0001`: invisible in the
        // harness, and renamed to `SK9030` in a report that still exits 0 (#298, #279, #295).
        //
        // ⚠ The pairing is also not merely out of bounds for such a call, it does not exist:
        // "the parameter the trailing argument fills" is not a fact once arguments outnumber
        // parameters, which is the same reasoning applied to named arguments above. Bounding on
        // `min(arguments, parameters)` is what SK2143 does with its own pair loop.
        var pairs = System.Math.Min(arguments.Count, method.Parameters.Length);
        var restated = 0;
        while (restated < pairs
               && arguments.Count - 1 - restated < method.Parameters.Length
               && RestatesItsDefault(
                   context,
                   arguments[arguments.Count - 1 - restated],
                   method.Parameters[arguments.Count - 1 - restated]
               )) {
            restated++;
        }

        // ⚠ Longest first. Two trailing defaults have to go in one edit or the rule fires again on
        // its own output, and `skala fix` makes one pass per finding.
        for (var drop = restated; drop >= 1; drop--) {
            if (!BindsToTheSameMethod(context, invocation, drop, method)) {
                continue;
            }

            var span = TextSpan.FromBounds(
                drop == arguments.Count
                    ? invocation.ArgumentList.OpenParenToken.Span.End
                    : arguments[arguments.Count - drop - 1].Span.End,
                invocation.ArgumentList.CloseParenToken.SpanStart
            );

            Report(
                context,
                span,
                string.Empty,
                drop == 1
                    ? "The argument restates the parameter's default value"
                    : "The last "
                    + drop.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " arguments restate their parameters' default values"
            );
            return;
        }
    }

    static bool RestatesItsDefault(
        SyntaxNodeAnalysisContext context,
        ArgumentSyntax argument,
        IParameterSymbol parameter
    ) {
        if (parameter.IsParams || !parameter.HasExplicitDefaultValue || HasCallerInfo(parameter)) {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
        return constant.HasValue && SameValue(constant.Value, parameter.ExplicitDefaultValue);
    }

    /// <summary>
    ///     ⚠ Whether two constants are the same value, and not merely the same boxed type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Equals</c> on its own compares the box first, so <c>Write(text, 0)</c> against
    ///         <c>Write(string text, long retries = 0)</c> never matched: the argument's constant is an
    ///         <c>int</c> and the parameter's default is a <c>long</c>. The same for every
    ///         <c>long</c>, <c>double</c>, <c>float</c> and <c>decimal</c> default written as a bare
    ///         <c>0</c> — the widest defaults in the BCL, and exactly the ones a caller restates. It was
    ///         a miss rather than a false positive and so left no trace at all (#298).
    ///     </para>
    ///     <para>
    ///         ⚠ <b><c>char</c> is deliberately not in the numeric set.</b> <c>Take('A')</c> against
    ///         <c>Take(int code = 65)</c> converts to the same number and is not the same sentence:
    ///         the author wrote a character, and deleting the argument would delete which of the two
    ///         they meant.
    ///     </para>
    /// </remarks>
    static bool SameValue(object? argument, object? parameterDefault) {
        if (Equals(argument, parameterDefault)) {
            return true;
        }

        if (argument is null || parameterDefault is null || !IsNumeric(argument) || !IsNumeric(parameterDefault)) {
            return false;
        }

        try {
            return Equals(
                System.Convert.ChangeType(
                    argument,
                    parameterDefault.GetType(),
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                parameterDefault
            );
        } catch (System.OverflowException) {
            return false;
        } catch (System.InvalidCastException) {
            return false;
        } catch (System.FormatException) {
            return false;
        }
    }

    static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    static bool HasCallerInfo(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            if (attribute.AttributeClass is { } type && System.Array.IndexOf(CallerInfo, type.Name) >= 0) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ The guard that makes the whole shape safe: re-bind the shortened call and demand the same
    ///     symbol.
    /// </summary>
    /// <remarks>
    ///     Roslyn's own <c>IDE0004</c> family is the standing reminder that "the types allow it" and "the
    ///     program means the same thing" are different questions. Speculative binding answers the second
    ///     one exactly, at the cost of one bind per candidate suffix.
    /// </remarks>
    static bool BindsToTheSameMethod(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        int drop,
        IMethodSymbol method
    ) {
        var kept = invocation.ArgumentList.Arguments.Take(invocation.ArgumentList.Arguments.Count - drop);
        var shortened = invocation.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(kept)));

        // ⚠ The rewrite is bound with no parent, and a member binding's receiver lives in the parent.
        // The same detached-node hazard that threw a NullReferenceException out of SK0234.
        if (!SpeculativeBinding.CanBindDetached(shortened)) {
            return false;
        }

        var speculated = context.SemanticModel.GetSpeculativeSymbolInfo(
            invocation.SpanStart,
            shortened,
            SpeculativeBindingOption.BindAsExpression
        );

        return SymbolEqualityComparer.Default.Equals(speculated.Symbol, method);
    }

    static void AnalyzeDelegateCreation(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (creation.Initializer is not null || creation.ArgumentList is not { Arguments.Count: 1 } list) {
            return;
        }

        var argument = list.Arguments[0];
        if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
            return;
        }

        // ⚠ A method group only. `new EventHandler(other)` where `other` is a delegate is a copy, and
        // a lambda inside a delegate creation is a different question about natural types.
        if (argument.Expression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)
            || context.SemanticModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol
            is not IMethodSymbol) {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type
            is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } type
            || !TargetTypeIs(context, creation, type)) {
            return;
        }

        Report(
            context,
            creation.Span,
            argument.Expression.ToString(),
            "The delegate creation is what the conversion would have done"
        );
    }

    static void AnalyzeLambda(SyntaxNodeAnalysisContext context) {
        var lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;
        var parameters = lambda.ParameterList.Parameters;
        if (lambda.ReturnType is not null || parameters.Count == 0) {
            return;
        }

        foreach (var parameter in parameters) {
            if (parameter.Type is null
                || parameter.Modifiers.Count > 0
                || parameter.AttributeLists.Count > 0
                || parameter.Default is not null) {
                return;
            }
        }

        if (context.SemanticModel.GetSymbolInfo(lambda, context.CancellationToken).Symbol
            is not IMethodSymbol written
            || context.SemanticModel.GetTypeInfo(lambda, context.CancellationToken).ConvertedType
            is not INamedTypeSymbol converted
            || Invoke(converted) is not { } invoke
            || invoke.Parameters.Length != parameters.Count
            || !TargetTypeIs(context, lambda, converted)) {
            return;
        }

        // ⚠ Nullability included. `Func<string?, int> f = (string x) => x.Length;` compiles, and
        // deleting the written type replaces a `string` the author asserted with the `string?` the
        // delegate declares — which is a CS8602 the fix would have introduced.
        for (var i = 0; i < invoke.Parameters.Length; i++) {
            if (written.Parameters[i].RefKind != RefKind.None
                || !SymbolEqualityComparer.IncludeNullability.Equals(
                    written.Parameters[i].Type,
                    invoke.Parameters[i].Type
                )) {
                return;
            }
        }

        Report(
            context,
            lambda.ParameterList.Span,
            Untyped(parameters),
            "The lambda parameter types are the ones the target already declares"
        );
    }

    /// <summary>
    ///     <c>delegate(int a) { … }</c> whose body never mentions <c>a</c>, where <c>delegate { … }</c>
    ///     is the same thing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ⚠
    ///         <b>
    ///             A parameterless <c>delegate</c> is convertible to a delegate type only when that
    ///             type has no <c>out</c> parameter
    ///         </b>, because the compiler would have nothing to assign
    ///         through. <c>ref</c> and <c>in</c> are fine; <c>out</c> is refused, and it is the one
    ///         difference between this rewrite compiling and not.
    ///     </para>
    ///     <para>
    ///         ⚠ Dropping the signature also *widens* what the expression converts to — that is the
    ///         whole point of the feature — so as an argument it can move overload resolution. The
    ///         target type therefore has to be written down, the same guard the other two conversion
    ///         shapes here use.
    ///     </para>
    /// </remarks>
    static void AnalyzeAnonymousMethod(SyntaxNodeAnalysisContext context) {
        var anonymous = (AnonymousMethodExpressionSyntax)context.Node;
        if (anonymous.ParameterList is not { Parameters.Count: > 0 } list) {
            return;
        }

        foreach (var parameter in list.Parameters) {
            if (parameter.AttributeLists.Count > 0 || parameter.Default is not null) {
                return;
            }

            foreach (var modifier in parameter.Modifiers) {
                if (modifier.IsKind(SyntaxKind.OutKeyword)) {
                    return;
                }
            }
        }

        if (context.SemanticModel.GetSymbolInfo(anonymous, context.CancellationToken).Symbol
            is not IMethodSymbol written
            || context.SemanticModel.GetTypeInfo(anonymous, context.CancellationToken).ConvertedType
            is not INamedTypeSymbol converted
            || Invoke(converted) is not { } invoke
            || invoke.Parameters.Length != list.Parameters.Count
            || !TargetTypeIs(context, anonymous, converted)) {
            return;
        }

        foreach (var parameter in written.Parameters) {
            if (IsMentioned(context, anonymous.Body, parameter)) {
                return;
            }
        }

        Report(
            context,
            list.Span,
            string.Empty,
            "The anonymous method's parameters are never used, so its signature says nothing"
        );
    }

    /// <summary>Whether a body reads or writes a parameter, asked of the symbol and not of the name.</summary>
    static bool IsMentioned(SyntaxNodeAnalysisContext context, SyntaxNode body, IParameterSymbol parameter) {
        foreach (var node in body.DescendantNodes()) {
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, parameter.Name, System.StringComparison.Ordinal)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol,
                    parameter
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>The delegate's <c>Invoke</c>, seeing through <c>Expression&lt;TDelegate&gt;</c>.</summary>
    static IMethodSymbol? Invoke(INamedTypeSymbol type) {
        if (type is { Name: "Expression", TypeArguments.Length: 1 }
            && type.ContainingNamespace.ToDisplayString() == "System.Linq.Expressions") {
            return (type.TypeArguments[0] as INamedTypeSymbol)?.DelegateInvokeMethod;
        }

        return type.DelegateInvokeMethod;
    }

    /// <summary>The same parameters with their types dropped, in the shortest legal spelling.</summary>
    static string Untyped(IReadOnlyList<ParameterSyntax> parameters) {
        if (parameters.Count == 1) {
            return parameters[0].Identifier.Text;
        }

        var builder = new StringBuilder("(");
        for (var i = 0; i < parameters.Count; i++) {
            if (i > 0) {
                builder.Append(", ");
            }

            builder.Append(parameters[i].Identifier.Text);
        }

        return builder.Append(')').ToString();
    }

    /// <summary>
    ///     Whether the expression sits under a target type the source writes down, and it is this one.
    /// </summary>
    /// <remarks>
    ///     ⚠ This is what keeps the two conversion shapes out of overload resolution entirely. As an
    ///     argument, both a method group and a lambda can be the thing that picks the overload, so
    ///     simplifying either could move the call; assigned to a declared type, there is no choice left
    ///     to change.
    /// </remarks>
    static bool TargetTypeIs(SyntaxNodeAnalysisContext context, ExpressionSyntax node, ITypeSymbol type) {
        switch (node.Parent) {
            case AssignmentExpressionSyntax assignment when assignment.Right == node:
                return SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type,
                    type
                );

            case EqualsValueClauseSyntax {
                Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration }
            }:
                return !declaration.Type.IsVar
                    && SymbolEqualityComparer.Default.Equals(
                        context.SemanticModel.GetTypeInfo(declaration.Type, context.CancellationToken).Type,
                        type
                    );

            default:
                return false;
        }
    }

    static void Report(SyntaxNodeAnalysisContext context, TextSpan span, string replacement, string message) {
        if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );
    }
}
