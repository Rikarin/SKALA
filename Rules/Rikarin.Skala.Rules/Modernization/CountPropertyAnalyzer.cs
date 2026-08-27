using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
/// <c>SK1034</c> — <c>items.Count()</c> and <c>items.Any()</c> where <c>items.Count</c> exists.
/// </summary>
/// <remarks>
/// Two shapes with two different safety arguments.
/// <list type="bullet">
/// <item>
/// <c>X.Count()</c> → <c>X.Count</c> is a same-type, same-value substitution and is safe wherever
/// it appears.
/// </item>
/// <item>
/// ⚠ <c>X.Any()</c> → <c>X.Count &gt; 0</c> changes an atom into a relational expression, so it is
/// only reported where the surrounding syntax cannot re-bind: a condition, an operand of
/// <c>&amp;&amp;</c>/<c>||</c>, a <c>!</c> (which becomes <c>== 0</c>), a return, or an argument.
/// Everywhere else the rule is silent rather than parenthesising, because a fix whose output needs
/// parentheses to stay correct is a fix one edit away from being wrong.
/// </item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CountPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CountPropertyOverLinq);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                var queryable = start.Compilation.GetTypeByMetadataName("System.Linq.IQueryable`1");
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable, queryable),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        INamedTypeSymbol? queryable
    ) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } access) {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        if (name != "Count" && name != "Any") {
            return;
        }

        var cancellation = context.CancellationToken;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        var definition = method.ReducedFrom ?? method.OriginalDefinition;
        if (!SymbolEqualityComparer.Default.Equals(definition.ContainingType, enumerable)
            || definition.Parameters.Length != 1) {
            return;
        }

        // ⚠ A name path, so that reading `.Count` runs exactly what `Count()` ran: nothing.
        if (!IsPlainNamePath(access.Expression)) {
            return;
        }

        var receiverType = model.GetTypeInfo(access.Expression, cancellation).Type;
        if (receiverType is null || receiverType.TypeKind == TypeKind.Error) {
            return;
        }

        // ⚠ On IQueryable, `Any()` is a query the provider translates and `Count` is not the same
        // round trip — often not even a legal one.
        if (queryable is not null && ImplementsQueryable(receiverType, queryable)) {
            return;
        }

        var property = CountProperty(receiverType);
        if (property is null) {
            return;
        }

        if (invocation.SpanContainsComment()) {
            return;
        }

        var receiver = access.Expression.ToString();
        if (name == "Count") {
            Report(
                context,
                invocation.Span,
                receiver + "." + property,
                "Use the `" + property + "` property instead of Enumerable.Count()"
            );
            return;
        }

        if (invocation.Parent is PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.LogicalNotExpression
            } negation) {
            Report(
                context,
                negation.Span,
                receiver + "." + property + " == 0",
                "Use `" + property + " == 0` instead of !Any()"
            );
            return;
        }

        if (!IsSafeBooleanPosition(invocation)) {
            return;
        }

        Report(
            context,
            invocation.Span,
            receiver + "." + property + " > 0",
            "Use `" + property + " > 0` instead of Any()"
        );
    }

    static void Report(SyntaxNodeAnalysisContext context, TextSpan span, string replacement, string message) {
        context.ReportDiagnostic(
            Diagnostic.Create(
                SkalaRule.Descriptor(RuleIds.CountPropertyOverLinq),
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                message
            )
        );
    }

    /// <summary>An accessible instance <c>Count</c> or <c>Length</c> of type <c>int</c>.</summary>
    static string? CountProperty(ITypeSymbol type) {
        foreach (var candidate in new[] { "Count", "Length" }) {
            for (var current = type; current is not null; current = current.BaseType) {
                foreach (var member in current.GetMembers(candidate)) {
                    if (member is IPropertySymbol {
                            IsStatic: false,
                            IsIndexer: false,
                            DeclaredAccessibility: Accessibility.Public,
                            GetMethod: not null
                        } property
                        && property.Type.SpecialType == SpecialType.System_Int32) {
                        return candidate;
                    }
                }
            }

            // An interface (`IReadOnlyCollection<T>`) has no base type; its Count is on an
            // implemented interface instead.
            foreach (var iface in type.AllInterfaces) {
                foreach (var member in iface.GetMembers(candidate)) {
                    if (member is IPropertySymbol { IsIndexer: false } property
                        && property.Type.SpecialType == SpecialType.System_Int32) {
                        return candidate;
                    }
                }
            }

            if (type is IArrayTypeSymbol && candidate == "Length") {
                return candidate;
            }
        }

        return null;
    }

    static bool ImplementsQueryable(ITypeSymbol type, INamedTypeSymbol queryable) {
        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, queryable)) {
            return true;
        }

        foreach (var iface in type.AllInterfaces) {
            if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, queryable)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ⚠ Where a relational expression may replace an atom without re-binding.
    /// </summary>
    static bool IsSafeBooleanPosition(InvocationExpressionSyntax invocation) =>
        invocation.Parent switch {
            IfStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            WhileStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            DoStatementSyntax statement => ReferenceEquals(statement.Condition, invocation),
            ConditionalExpressionSyntax conditional => ReferenceEquals(conditional.Condition, invocation),
            BinaryExpressionSyntax {
                RawKind: (int)SyntaxKind.LogicalAndExpression or (int)SyntaxKind.LogicalOrExpression
            } => true,
            ParenthesizedExpressionSyntax => true,
            ReturnStatementSyntax => true,
            ArgumentSyntax => true,
            _ => false
        };

    static bool IsPlainNamePath(ExpressionSyntax expression) {
        while (true) {
            switch (expression) {
                case IdentifierNameSyntax:
                case ThisExpressionSyntax:
                case BaseExpressionSyntax:
                    return true;

                case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                    expression = access.Expression;
                    continue;

                default:
                    return false;
            }
        }
    }
}

/// <summary>A comment inside a span the fix replaces is content the fix would delete.</summary>
internal static class SyntaxSpanExtensions {
    public static bool SpanContainsComment(this SyntaxNode node) {
        foreach (var trivia in node.DescendantTrivia()) {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)) {
                return true;
            }
        }

        return false;
    }
}
