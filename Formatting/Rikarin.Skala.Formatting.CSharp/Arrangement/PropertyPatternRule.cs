using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Rikarin.Skala.Formatting.CSharp.Arrangement;

/// <summary>Combines boolean member tests on a non-null, stable receiver into a property pattern.</summary>
public sealed class PropertyPatternRule : ArrangementRule {
    public override string Id => ArrangeIds.PropertyPattern;
    public override bool NeedsSemantics => true;
    public override bool IsEnabled(in ArrangementOptions options) => true;

    public override SyntaxNode Apply(ArrangementContext context) =>
        context.Semantics.Compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp8 }
            ? new Rewriter(context.Guard, context.Semantics).Visit(context.Root)
            : context.Root;

    sealed class Rewriter(FormatterTagGuard guard, SemanticModel model) : GuardedRewriter(guard) {
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node) {
            if (!node.IsKind(SyntaxKind.LogicalAndExpression)
                || node.ContainsDirectives
                || node.DescendantTrivia()
                    .Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                    )
                || node.Ancestors().Any(static ancestor => ancestor is QueryExpressionSyntax)
                || IsExpressionTree(node)) {
                return base.VisitBinaryExpression(node);
            }

            var terms = new List<ExpressionSyntax>();
            Flatten(node, terms);
            var clauses = new List<string>();
            var members = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            ExpressionSyntax? receiver = null;
            ISymbol? receiverSymbol = null;
            foreach (var term in terms) {
                if (!TryMember(term, out var member, out var expected)
                    || model.GetSymbolInfo(member).Symbol is not { } symbol
                    || !IsPlainBooleanMember(symbol)
                    || !members.Add(symbol)) {
                    return base.VisitBinaryExpression(node);
                }

                var candidate = Unwrap(member.Expression);
                var candidateSymbol = model.GetSymbolInfo(candidate).Symbol;
                if (receiver is null) {
                    if (candidate is not ThisExpressionSyntax
                        && candidateSymbol is not (ILocalSymbol { RefKind: RefKind.None }
                            or IParameterSymbol { RefKind: RefKind.None })) {
                        return base.VisitBinaryExpression(node);
                    }

                    var type = model.GetTypeInfo(candidate);
                    if (type.Type is null
                        || type.Type.TypeKind == TypeKind.Dynamic
                        || type.Type.IsReferenceType
                        && type.Nullability.FlowState != NullableFlowState.NotNull) {
                        return base.VisitBinaryExpression(node);
                    }

                    receiver = candidate;
                    receiverSymbol = candidateSymbol;
                } else if (!SyntaxFactory.AreEquivalent(receiver, candidate)
                           || !SymbolEqualityComparer.Default.Equals(receiverSymbol, candidateSymbol)) {
                    return base.VisitBinaryExpression(node);
                }

                clauses.Add(member.Name + ": " + (expected ? "true" : "false"));
            }

            var pattern = SyntaxFactory.ParseExpression(receiver + " is { " + string.Join(", ", clauses) + " }");
            return (NeedsParentheses(node) ? SyntaxFactory.ParenthesizedExpression(pattern) : pattern)
                .WithTriviaFrom(node);
        }

        static bool NeedsParentheses(ExpressionSyntax node) =>
            node.Parent switch {
                IfStatementSyntax
                    or WhileStatementSyntax
                    or DoStatementSyntax
                    or ForStatementSyntax
                    or ReturnStatementSyntax
                    or ArrowExpressionClauseSyntax
                    or EqualsValueClauseSyntax
                    or ParenthesizedExpressionSyntax
                    or ArgumentSyntax => false,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression)
                    || binary.IsKind(SyntaxKind.LogicalOrExpression) => false,
                _ => true
            };

        bool TryMember(ExpressionSyntax term, out MemberAccessExpressionSyntax member, out bool expected) {
            term = Unwrap(term);
            expected = true;
            if (term is PrefixUnaryExpressionSyntax negation
                && negation.IsKind(SyntaxKind.LogicalNotExpression)
                && model.GetOperation(negation) is IUnaryOperation { OperatorMethod: null }) {
                expected = false;
                term = Unwrap(negation.Operand);
            }

            member = term as MemberAccessExpressionSyntax ?? null!;
            return member is not null && member.IsKind(SyntaxKind.SimpleMemberAccessExpression);
        }

        void Flatten(ExpressionSyntax expression, List<ExpressionSyntax> terms) {
            expression = Unwrap(expression);
            if (expression is BinaryExpressionSyntax binary
                && binary.IsKind(SyntaxKind.LogicalAndExpression)
                && model.GetOperation(binary) is IBinaryOperation { OperatorMethod: null }) {
                Flatten(binary.Left, terms);
                Flatten(binary.Right, terms);
            } else {
                terms.Add(expression);
            }
        }

        static bool IsPlainBooleanMember(ISymbol symbol) {
            if (symbol is IFieldSymbol field) {
                return !field.IsStatic && !field.IsVolatile && field.Type.SpecialType == SpecialType.System_Boolean;
            }

            if (symbol is not IPropertySymbol {
                    IsStatic: false,
                    IsVirtual: false,
                    IsOverride: false,
                    IsAbstract: false,
                    RefKind: RefKind.None,
                    Type.SpecialType: SpecialType.System_Boolean,
                    GetMethod: { IsExtern: false } getter
                } property) {
                return false;
            }

            // Pattern matching may reorder reads. Only compiler-generated getters are eligible.
            foreach (var reference in property.DeclaringSyntaxReferences) {
                if (reference.GetSyntax() is ParameterSyntax && property.ContainingType.IsRecord) {
                    return true;
                }

                if (reference.GetSyntax() is PropertyDeclarationSyntax {
                        ExpressionBody: null,
                        AccessorList: { } list
                    } declaration
                    && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                    && list.Accessors.All(a => a.Body is null && a.ExpressionBody is null)) {
                    return true;
                }
            }

            return property.DeclaringSyntaxReferences.IsEmpty
                && getter.GetAttributes()
                    .Any(a =>
                        a.AttributeClass?.ToDisplayString()
                        == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
                    );
        }

        bool IsExpressionTree(SyntaxNode node) =>
            node.Ancestors()
                .OfType<AnonymousFunctionExpressionSyntax>()
                .Any(lambda => model.GetTypeInfo(lambda).ConvertedType is INamedTypeSymbol type
                    && type.OriginalDefinition.Name == "Expression"
                    && type.ContainingNamespace.ToDisplayString() == "System.Linq.Expressions"
                );

        static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
            while (expression is ParenthesizedExpressionSyntax parentheses) {
                expression = parentheses.Expression;
            }

            return expression;
        }
    }
}
