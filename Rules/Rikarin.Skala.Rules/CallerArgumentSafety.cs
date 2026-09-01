using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>Expression text is observable when an enclosing argument uses caller-expression capture.</summary>
internal static class CallerArgumentSafety {
    public static bool CapturesText(SemanticModel model, SyntaxNode node, CancellationToken cancellation) {
        foreach (var argument in node.Ancestors().OfType<ArgumentSyntax>()) {
            if (model.GetOperation(argument, cancellation) is not IArgumentOperation { Parameter: { } parameter }) {
                continue;
            }

            var parameters = parameter.ContainingSymbol switch {
                IMethodSymbol method => method.Parameters,
                IPropertySymbol property => property.Parameters,
                _ => default
            };
            if (!parameters.IsDefault
                && parameters.Any(static candidate => candidate.GetAttributes()
                        .Any(static attribute =>
                            attribute.AttributeClass?.ToDisplayString()
                            == "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"
                        )
                )) {
                return true;
            }
        }

        return false;
    }
}
