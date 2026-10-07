using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules;

/// <summary>Expression text is observable when an enclosing argument uses caller-expression capture.</summary>
/// <remarks>
///     ⚠ <b>Two questions, and only the second one is universal.</b> <see cref="CapturesText" /> is the
///     old per-rule question — "is this node under an argument of a method that has a
///     <c>[CallerArgumentExpression]</c> parameter anywhere" — asked by the five rules that decline the
///     whole finding. <see cref="ChangesCapturedText" /> is the question every fix is asked on its way
///     out of the analyzer host (#422): does this edit touch text the compiler will hand to a
///     <c>[CallerArgumentExpression]</c> parameter at run time.
/// </remarks>
public static class CallerArgumentSafety {
    const string AttributeName = "System.Runtime.CompilerServices.CallerArgumentExpressionAttribute";

    internal static bool CapturesText(SemanticModel model, SyntaxNode node, CancellationToken cancellation) {
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
                        .Any(static attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName)
                )) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether replacing <paramref name="edit" /> would change a string the compiler captures through
    ///     <c>[CallerArgumentExpression]</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Precise about what is captured, conservative about what an edit does to it.</b> A capture
    ///     exists only where the compiler supplies the caller-expression argument itself — an explicit
    ///     <c>message</c> captures nothing — and only when the attribute names a parameter that exists and
    ///     is not itself: a misspelled name is <c>CS8963</c>, a warning, and captures nothing. The text
    ///     captured is the bound argument's expression, which for a reduced extension call is the
    ///     receiver, for a <c>params</c> parameter is the whole call (measured), and for a C# 14 extension
    ///     block is the instance. Any edit that overlaps that text <b>or abuts it</b> is reported,
    ///     including one that replaces the whole call: whether a rewrite re-emits the argument verbatim
    ///     is not something a span can say, and a declined safe fix is still offered for review.
    ///     <para>
    ///         The call-shaped nodes are found by span, not by walking up from the edit, because an
    ///         edit can lie inside an argument (the usual case), contain the whole call, or contain a
    ///         nested call whose argument is captured.
    ///     </para>
    /// </remarks>
    public static bool ChangesCapturedText(SemanticModel model, TextSpan edit, CancellationToken cancellation) {
        var root = model.SyntaxTree.GetRoot(cancellation);
        if (edit.Start < 0 || edit.End > root.FullSpan.End) {
            return false;
        }

        // ⚠ Widened by one character each way: `DescendantNodesAndSelf` returns nothing at all for an
        // empty span, and an insertion — `SK4020`'s `static ` — is exactly an empty span. Measured: every
        // insertion in the #422 probe passed as safe until this line. `Touches` below is the real test.
        var query = TextSpan.FromBounds(
            System.Math.Max(0, edit.Start - 1),
            System.Math.Min(root.FullSpan.End, edit.End + 1)
        );
        foreach (var node in root.DescendantNodesAndSelf(query)) {
            if (!IsCallShaped(node) || !Touches(node.Span, edit)) {
                continue;
            }

            foreach (var captured in CapturedSpans(model, node, cancellation)) {
                if (Touches(captured, edit)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Overlap, or contact at either end: an insertion at the edge of the text joins it.</summary>
    static bool Touches(TextSpan captured, TextSpan edit) => edit.Start <= captured.End && edit.End >= captured.Start;

    static bool IsCallShaped(SyntaxNode node) =>
        node.Kind()
            is SyntaxKind.InvocationExpression
            or SyntaxKind.ObjectCreationExpression
            or SyntaxKind.ImplicitObjectCreationExpression
            or SyntaxKind.BaseConstructorInitializer
            or SyntaxKind.ThisConstructorInitializer
            or SyntaxKind.PrimaryConstructorBaseType
            or SyntaxKind.ElementAccessExpression
            or SyntaxKind.Attribute;

    static IEnumerable<TextSpan> CapturedSpans(SemanticModel model, SyntaxNode call, CancellationToken cancellation) {
        var operation = model.GetOperation(call, cancellation);
        if (operation is IAttributeOperation attribute) {
            operation = attribute.Operation;
        }

        var (arguments, instance) = operation switch {
            IInvocationOperation invocation => (invocation.Arguments, invocation.Instance),
            IObjectCreationOperation creation => (creation.Arguments, null),
            IPropertyReferenceOperation property => (property.Arguments, property.Instance),
            _ => (default, (IOperation?)null)
        };
        if (arguments.IsDefaultOrEmpty) {
            yield break;
        }

        foreach (var supplied in arguments) {
            // ⚠ Only where the compiler supplies the value. An argument the caller wrote is the
            // caller's string, and nothing an edit does elsewhere moves it.
            if (supplied is not { ArgumentKind: ArgumentKind.DefaultValue, Parameter: { } parameter }
                || CapturedName(parameter) is not { } name
                || name == parameter.Name) {
                continue;
            }

            var found = false;
            foreach (var argument in arguments) {
                if (argument.Parameter?.Name != name || ReferenceEquals(argument, supplied)) {
                    continue;
                }

                found = true;
                if (Source(argument, call) is { } span) {
                    yield return span;
                }
            }

            // A C# 14 extension block's receiver is not a parameter of the member; it is the instance.
            if (!found
                && instance is { IsImplicit: false }
                && parameter.ContainingSymbol is IMethodSymbol { ContainingType: { IsExtension: true } extension }
                && extension.ExtensionParameter?.Name == name) {
                yield return instance.Syntax.Span;
            }
        }
    }

    static string? CapturedName(IParameterSymbol parameter) {
        foreach (var attribute in parameter.GetAttributes()) {
            if (attribute.AttributeClass?.ToDisplayString() == AttributeName
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string name) {
                return name;
            }
        }

        return null;
    }

    /// <summary>The source text an argument contributes, or nothing when it contributes none.</summary>
    static TextSpan? Source(IArgumentOperation argument, SyntaxNode call) {
        // ⚠ Measured, not assumed: for `Many(values: x => x + 5)` the compiler captures the whole
        // call, `Captures.Many(values: x => x + 5)`, not the argument — so a `params` capture is
        // answered with the call's span, which also covers every element of an expanded list.
        if (argument.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection) {
            return call.Span;
        }

        if (argument.Syntax is ArgumentSyntax written) {
            return written.Expression.Span;
        }

        if (argument.Syntax is AttributeArgumentSyntax attributeArgument) {
            return attributeArgument.Expression.Span;
        }

        // A reduced extension method's receiver: implicit as an argument, explicit as text.
        return argument.ArgumentKind == ArgumentKind.DefaultValue ? null : argument.Value.Syntax.Span;
    }
}
