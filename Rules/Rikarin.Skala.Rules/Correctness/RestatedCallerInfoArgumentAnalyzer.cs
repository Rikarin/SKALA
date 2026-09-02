using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2141</c> — an argument that suppresses the caller-info substitution it sits on top of.
/// </summary>
/// <remarks>
///     <para>
///         Caller-info substitution happens only where the argument is <em>omitted</em>. Supply one and
///         the compiler steps back without a word, so a <c>[CallerLineNumber]</c> becomes whatever the
///         author typed and every log line, assertion and exception built from it names a place the code
///         is not.
///     </para>
///     <para>
///         ⚠ <b>The general shape does not ship and the narrowing is the rule.</b>
///         <c>OnPropertyChanged(nameof(Other))</c> against a <c>[CallerMemberName]</c> parameter is
///         ordinary correct code — naming a different property is what the overload is for — so a name
///         or expression argument is reported only when it restates <em>exactly</em> what the compiler
///         would have substituted. A location argument has no such deliberate use and is reported for
///         any constant. Forwarding needs no guard of its own: a relay passes an identifier, and an
///         identifier is not a constant.
///     </para>
///     <para>
///         ⚠ <b>This rule and <c>SK0232</c> are disjoint by construction, not by filter.</b>
///         <c>SK0232</c> excludes caller-info parameters outright, because passing <c>null</c> to one is
///         the opposite of redundant — the argument is the only thing keeping the value null. Nothing
///         here reports <c>null</c>, so the two can never disagree about a span.
///     </para>
///     <para>
///         ⚠ <b>A trailing run, dropped in one edit</b>, for the reason <c>SK0232</c> gives: two
///         adjacent caller-info arguments reported separately make <c>skala fix</c> report its own
///         output. And <c>#298</c>'s lesson is taken directly — the index is bounded, not the counter,
///         so an expanded <c>params</c> call cannot walk off the parameter array.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RestatedCallerInfoArgumentAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RestatedCallerInfoArgument);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression);
    }

    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        Analyze(context, invocation, invocation.ArgumentList);
    }

    static void AnalyzeCreation(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (creation.ArgumentList is { } list) {
            Analyze(context, creation, list);
        }
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ExpressionSyntax call, ArgumentListSyntax list) {
        var arguments = list.Arguments;
        if (arguments.Count == 0
            || context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        // ⚠ Positional only, and never more arguments than parameters. A named argument does not have
        // to be last and does not have to fill the parameter its position names; an expanded `params`
        // call has more arguments than there are parameters at all. Under either, "the parameter this
        // argument fills" stops being a fact — which is exactly what #298 found SK0232 assuming.
        if (arguments.Count > method.Parameters.Length) {
            return;
        }

        foreach (var argument in arguments) {
            if (argument.NameColon is not null || !argument.RefKindKeyword.IsKind(SyntaxKind.None)) {
                return;
            }
        }

        var member = EnclosingMemberName(call);
        var suppressed = 0;
        while (suppressed < arguments.Count
               && Suppresses(
                   context,
                   arguments,
                   arguments.Count - 1 - suppressed,
                   method,
                   member
               )) {
            suppressed++;
        }

        // ⚠ Longest first, and the whole run in one edit. Deleting a caller-info argument can change
        // which overload binds, so every candidate suffix is re-bound speculatively before it is
        // offered as a fix.
        for (var drop = suppressed; drop >= 1; drop--) {
            if (!BindsToTheSameMethod(context, call, list, drop, method)) {
                continue;
            }

            var span = TextSpan.FromBounds(
                drop == arguments.Count
                    ? list.OpenParenToken.Span.End
                    : arguments[arguments.Count - drop - 1].Span.End,
                list.CloseParenToken.SpanStart
            );

            if (RewriteGuards.ContainsCommentOrDirective(context.Node.SyntaxTree, span)) {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    Location.Create(context.Node.SyntaxTree, span),
                    FixEdits.Pack((span, string.Empty)),
                    drop == 1
                        ? "The argument replaces the value the caller-info attribute would have supplied"
                        : "The last "
                        + drop.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " arguments replace the values the caller-info attributes would have supplied"
                )
            );

            return;
        }
    }

    /// <summary>
    ///     Whether this argument is a caller-info value the compiler was going to supply anyway, or a
    ///     fabricated source location.
    /// </summary>
    static bool Suppresses(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        int index,
        IMethodSymbol method,
        string? member
    ) {
        var parameter = method.Parameters[index];
        if (parameter.IsParams) {
            return false;
        }

        var kind = CallerInfo(parameter, out var expressionFor);
        if (kind is null) {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(
            arguments[index].Expression,
            context.CancellationToken
        );

        // A forwarded caller-info parameter is an identifier, never a constant, so a relay is
        // declined here without needing a rule of its own.
        if (!constant.HasValue || constant.Value is null) {
            return false;
        }

        switch (kind) {
            // ⚠ No deliberate use exists: nobody hand-writes the line number or the path they mean.
            case "CallerLineNumberAttribute":
            case "CallerFilePathAttribute":
                return true;

            case "CallerMemberNameAttribute":
                return member is not null
                    && constant.Value is string name
                    && string.Equals(name, member, System.StringComparison.Ordinal);

            default:
                return constant.Value is string written
                    && Source(arguments, method, expressionFor) is { } text
                    && string.Equals(written, text, System.StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     The caller-info attribute on this parameter, and for
    ///     <c>[CallerArgumentExpression]</c> the parameter name it quotes.
    /// </summary>
    static string? CallerInfo(IParameterSymbol parameter, out string? expressionFor) {
        expressionFor = null;
        foreach (var attribute in parameter.GetAttributes()) {
            var name = attribute.AttributeClass?.Name;
            switch (name) {
                case "CallerMemberNameAttribute":
                case "CallerLineNumberAttribute":
                case "CallerFilePathAttribute":
                    return name;

                case "CallerArgumentExpressionAttribute":
                    if (attribute.ConstructorArguments.Length == 1
                        && attribute.ConstructorArguments[0].Value is string target) {
                        expressionFor = target;
                        return name;
                    }

                    return null;
            }
        }

        return null;
    }

    /// <summary>
    ///     The source text of the argument filling the parameter <c>[CallerArgumentExpression]</c>
    ///     names, which is what the compiler would have substituted.
    /// </summary>
    /// <remarks>
    ///     Every argument is positional and there are no more of them than there are parameters — both
    ///     established before this is reached — so the parameter's index is the argument's index. A
    ///     parameter whose argument was omitted would have had <c>""</c> substituted, and returning
    ///     null for it declines rather than matching an empty literal by accident.
    /// </remarks>
    static string? Source(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol method,
        string? parameterName
    ) {
        if (parameterName is null) {
            return null;
        }

        for (var i = 0; i < method.Parameters.Length; i++) {
            if (string.Equals(method.Parameters[i].Name, parameterName, System.StringComparison.Ordinal)) {
                return i < arguments.Count ? arguments[i].Expression.ToString() : null;
            }
        }

        return null;
    }

    /// <summary>The name <c>[CallerMemberName]</c> would have substituted at this call.</summary>
    /// <remarks>
    ///     ⚠ A call inside a local function is declined outright rather than credited to the containing
    ///     member. A lambda is not: it has no name of its own and the containing member's is
    ///     unambiguous. Anything that is not a method, accessor, property or indexer — a field
    ///     initializer, where the substitution is <c>.ctor</c> — is declined for the same reason, which
    ///     costs nothing because no source writes <c>".ctor"</c> by hand.
    /// </remarks>
    static string? EnclosingMemberName(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case LocalFunctionStatementSyntax:
                    return null;

                case MethodDeclarationSyntax method:
                    return method.Identifier.Text;

                case PropertyDeclarationSyntax property:
                    return property.Identifier.Text;

                case EventDeclarationSyntax declaredEvent:
                    return declaredEvent.Identifier.Text;

                case IndexerDeclarationSyntax:
                    return "Item";

                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case OperatorDeclarationSyntax:
                case ConversionOperatorDeclarationSyntax:
                case BaseTypeDeclarationSyntax:
                    return null;
            }
        }

        return null;
    }

    static bool BindsToTheSameMethod(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax call,
        ArgumentListSyntax list,
        int drop,
        IMethodSymbol method
    ) {
        var kept = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(list.Arguments.Take(list.Arguments.Count - drop))
        );

        var shortened = call switch {
            InvocationExpressionSyntax invocation => (ExpressionSyntax)invocation.WithArgumentList(kept),
            ObjectCreationExpressionSyntax creation => creation.WithArgumentList(kept),
            _ => null
        };

        if (shortened is null) {
            return false;
        }

        var speculated = context.SemanticModel.GetSpeculativeSymbolInfo(
            call.SpanStart,
            shortened,
            SpeculativeBindingOption.BindAsExpression
        );

        return SymbolEqualityComparer.Default.Equals(speculated.Symbol, method);
    }
}
