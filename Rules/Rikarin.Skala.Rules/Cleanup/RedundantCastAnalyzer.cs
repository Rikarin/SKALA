using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Cleanup;

/// <summary>
///     <c>SK0234</c> — a conversion that converts nothing.
/// </summary>
/// <remarks>
///     <para>
///         Four shapes: a cast whose operand already has the cast's type, explicit method type arguments
///         inference reproduces exactly, an array size that repeats the element count, and tuple
///         component names the declared type already carries.
///     </para>
///     <para>
///         ⚠
///         <b>
///             A cast is not redundant merely because the types allow removing it, and this rule is
///             written around that sentence.
///         </b> <c>var x = (long)1;</c> types <c>x</c> as <c>long</c> and
///         <c>var x = 1;</c> types it as <c>int</c>. <c>M((object)s)</c> and <c>M(s)</c> call different
///         overloads. <c>flag ? (long)a : b</c> infers a different type once the cast goes. Roslyn's own
///         <c>IDE0004</c> is the standing example of getting this wrong, so Skala covers the one subset
///         where none of those questions can arise: an <b>identity</b> conversion, where the operand's
///         type and the cast's type are the same symbol including nullability. The expression's type is
///         then unchanged by the deletion, so nothing downstream of it can change either. Every widening,
///         narrowing and boxing cast in the family is deliberately left uncovered.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantCastAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantCast);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
        context.RegisterSyntaxNodeAction(AnalyzeTypeArguments, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeArraySize, SyntaxKind.ArrayCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeTupleNames, SyntaxKind.TupleExpression);
    }

    static void AnalyzeCast(SyntaxNodeAnalysisContext context) {
        var cast = (CastExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        if (model.GetTypeInfo(cast.Type, context.CancellationToken).Type is not { } target
            || model.GetTypeInfo(cast.Expression, context.CancellationToken).Type is not { } source
            || target.TypeKind == TypeKind.Error
            || source.TypeKind == TypeKind.Error) {
            return;
        }

        // ⚠ Tuples are excluded from this branch and handled by the component-name branch instead.
        // `((int a, int b))(1, 2)` and `((int, int))(1, 2)` differ in the *names* the result carries,
        // and whether a symbol comparison sees that difference is not something to be guessing about
        // in the one rule where a wrong answer changes a program.
        if (target.IsTupleType || source.IsTupleType) {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(source, target)
            || !model.ClassifyConversion(cast.Expression, target).IsIdentity
            || !SameNullability(model, cast, source)) {
            return;
        }

        Report(
            context,
            TextSpan.FromBounds(cast.OpenParenToken.SpanStart, cast.Expression.SpanStart),
            string.Empty,
            "The cast's operand already has the cast's type"
        );
    }

    /// <summary>
    ///     ⚠ Whether the cast is doing nullable work, asked twice because one question is not enough.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The annotation cannot be read off the target symbol.</b> <c>GetTypeInfo</c> on a
    ///         <c>TypeSyntax</c> returns <see cref="NullableAnnotation.None" /> for every written type —
    ///         <c>string</c> and <c>string?</c> alike — so a comparison through
    ///         <c>SymbolEqualityComparer.IncludeNullability</c> rejects <em>every</em> cast, including the
    ///         identity ones. Measured, not assumed: it silently reported nothing at all. The written
    ///         annotation therefore comes from the syntax, where it is unambiguous.
    ///     </para>
    ///     <para>
    ///         The flow state is the second question and it is a different one. The annotation says what
    ///         was written; the flow state says what the compiler had concluded at that point, which is
    ///         what a <c>var</c> declared from the cast would capture.
    ///     </para>
    /// </remarks>
    static bool SameNullability(SemanticModel model, CastExpressionSyntax cast, ITypeSymbol source) =>
        cast.Type is NullableTypeSyntax == (source.NullableAnnotation == NullableAnnotation.Annotated)
        && model.GetTypeInfo(cast.Expression).Nullability.FlowState
        == model.GetTypeInfo(cast).Nullability.FlowState;

    static void AnalyzeTypeArguments(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var generic = invocation.Expression switch {
            GenericNameSyntax name => name,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
            _ => null
        };

        if (generic is null
            || generic.TypeArgumentList.Arguments.Count == 0
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method) {
            return;
        }

        // ⚠ The same question SK0232 asks about a deleted argument, and for the same reason: type
        // inference may reach a different constructed method, or none at all. Only an identical
        // constructed symbol counts — not the same definition with different type arguments.
        var inferred = invocation.ReplaceNode(generic, SyntaxFactory.IdentifierName(generic.Identifier));
        var speculated = context.SemanticModel.GetSpeculativeSymbolInfo(
            invocation.SpanStart,
            inferred,
            SpeculativeBindingOption.BindAsExpression
        );

        if (!SymbolEqualityComparer.Default.Equals(speculated.Symbol, method)) {
            return;
        }

        Report(
            context,
            generic.TypeArgumentList.Span,
            string.Empty,
            "Type inference reaches the same method without the explicit type arguments"
        );
    }

    static void AnalyzeArraySize(SyntaxNodeAnalysisContext context) {
        var creation = (ArrayCreationExpressionSyntax)context.Node;
        if (creation.Initializer is not { } initializer || creation.Type.RankSpecifiers.Count != 1) {
            return;
        }

        // One dimension only. A jagged or rectangular creation has sizes whose relationship to the
        // initializer's shape is not the flat count this branch compares against.
        var rank = creation.Type.RankSpecifiers[0];
        if (rank.Sizes.Count != 1
            || rank.Sizes[0] is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } size
            || size.Token.Value is not int declared
            || declared != initializer.Expressions.Count) {
            return;
        }

        Report(context, size.Span, string.Empty, "The array size is the number of elements written");
    }

    static void AnalyzeTupleNames(SyntaxNodeAnalysisContext context) {
        var tuple = (TupleExpressionSyntax)context.Node;

        // ⚠ The declared type has to be *written*, and written as a tuple type. Under `var` the names
        // come from the literal and deleting them deletes them from the variable's type; there is no
        // semantic query that separates those two cases, because the resulting type is identical.
        if (tuple.Parent is not EqualsValueClauseSyntax {
                Parent:
                VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: TupleTypeSyntax declared } }
            }
            || declared.Elements.Count != tuple.Arguments.Count) {
            return;
        }

        var edits = new List<(TextSpan Span, string Text)>(tuple.Arguments.Count);
        for (var i = 0; i < tuple.Arguments.Count; i++) {
            if (tuple.Arguments[i].NameColon is not { } name) {
                continue;
            }

            if (!declared.Elements[i].Identifier.IsKind(SyntaxKind.IdentifierToken)
                || !string.Equals(
                    declared.Elements[i].Identifier.Text,
                    name.Name.Identifier.Text,
                    StringComparison.Ordinal
                )) {
                return;
            }

            edits.Add((TextSpan.FromBounds(name.SpanStart, tuple.Arguments[i].Expression.SpanStart), string.Empty));
        }

        if (edits.Count == 0 || RewriteGuards.ContainsCommentOrDirective(tuple.SyntaxTree, tuple.Span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                tuple.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "The tuple component names are the ones the declared type already carries"
            )
        );
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
