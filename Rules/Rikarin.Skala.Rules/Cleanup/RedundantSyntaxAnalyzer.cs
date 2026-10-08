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
///     <c>SK0233</c> — nine token-level deletions that change nothing but the reading.
/// </summary>
/// <remarks>
///     <para>
///         The cheapest rules in the parity map and the ones doc 17 § "A large share of the 580 is
///         cheap" is about: an empty attribute argument list, a single lambda parameter's parentheses,
///         parentheses around a whole pattern, a semicolon after a braced declaration, braces around a
///         one-argument collection element, an anonymous-type property name that restates the
///         expression, <c>ascending</c> in a query, a range bound that is already the default, and an
///         empty property pattern under a type.
///     </para>
///     <para>
///         ⚠ <b><c>SK0209</c> is not being duplicated.</b> That rule removes redundant
///         <em>expression</em> parentheses through <c>skala_parentheses_redundancy_style</c>, and it
///         matches <c>ParenthesizedExpressionSyntax</c> and nothing else. The three parenthesis shapes
///         here are an attribute argument list, a lambda parameter list and a parenthesized
///         <em>pattern</em> — three different nodes, none of which that option can reach.
///     </para>
///     <para>
///         ⚠ <b>Purely syntactic on purpose.</b> Not one branch reads the semantic model, so this is the
///         one rule in the <c>SK023x</c> family that still runs — and is therefore still measurable —
///         under <c>--load=loose</c>, where a dependency-less source slice makes every semantic guard
///         withdraw. That is also why the discard-designation shape is left out: <c>out var _</c> becomes
///         <c>out _</c> only where nothing named <c>_</c> is in scope, and answering that needs a lookup.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantSyntaxAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RedundantSyntax);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
        registrar.RegisterSyntaxNodeAction(AnalyzeLambda, SyntaxKind.ParenthesizedLambdaExpression);
        registrar.RegisterSyntaxNodeAction(AnalyzePatternParentheses, SyntaxKind.ParenthesizedPattern);
        registrar.RegisterSyntaxNodeAction(
            AnalyzeDeclarationSemicolon,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.NamespaceDeclaration
        );
        registrar.RegisterSyntaxNodeAction(AnalyzeElementBraces, SyntaxKind.ComplexElementInitializerExpression);
        registrar.RegisterSyntaxNodeAction(AnalyzeAnonymousMember, SyntaxKind.AnonymousObjectMemberDeclarator);
        registrar.RegisterSyntaxNodeAction(AnalyzeOrdering, SyntaxKind.AscendingOrdering);
        registrar.RegisterSyntaxNodeAction(AnalyzeRange, SyntaxKind.RangeExpression);
        registrar.RegisterSyntaxNodeAction(AnalyzePropertyPattern, SyntaxKind.RecursivePattern);
    }

    static void AnalyzeAttribute(SyntaxNodeAnalysisContext context) {
        if (((AttributeSyntax)context.Node).ArgumentList is not { Arguments.Count: 0 } list) {
            return;
        }

        var attribute = (AttributeSyntax)context.Node;
        Report(
            context,
            attribute,
            attribute.WithArgumentList(null),
            list.Span,
            string.Empty,
            "The attribute's argument list is empty"
        );
    }

    static void AnalyzeLambda(SyntaxNodeAnalysisContext context) {
        var lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;
        if (lambda.ReturnType is not null || lambda.ParameterList.Parameters.Count != 1) {
            return;
        }

        // ⚠ A type, a modifier, an attribute or a default value all make the parentheses part of the
        // grammar rather than decoration. The typed case belongs to SK0232, which produces this
        // shape's *output*, so the two are disjoint by construction rather than by luck.
        var parameter = lambda.ParameterList.Parameters[0];
        if (parameter.Type is not null
            || parameter.Modifiers.Count > 0
            || parameter.AttributeLists.Count > 0
            || parameter.Default is not null) {
            return;
        }

        Report(
            context,
            lambda,
            SyntaxFactory.SimpleLambdaExpression(
                lambda.AttributeLists,
                lambda.Modifiers,
                parameter,
                lambda.ArrowToken,
                lambda.Block,
                lambda.ExpressionBody
            ),
            lambda.ParameterList.Span,
            parameter.Identifier.Text,
            "A single untyped lambda parameter needs no parentheses"
        );
    }

    static void AnalyzePatternParentheses(SyntaxNodeAnalysisContext context) {
        var parenthesized = (ParenthesizedPatternSyntax)context.Node;

        // ⚠ Only where the pattern is the *whole* of its position. Inside an `and`/`or` the
        // parentheses can be the precedence, and proving otherwise is a re-parse this rule does not
        // do — SK0209 exists precisely because that proof is expensive.
        if (parenthesized.Parent is not (IsPatternExpressionSyntax
            or SwitchExpressionArmSyntax
            or CasePatternSwitchLabelSyntax
            or SubpatternSyntax)) {
            return;
        }

        Report(
            context,
            parenthesized,
            parenthesized.Pattern,
            parenthesized.Span,
            parenthesized.Pattern.ToString(),
            "The pattern's parentheses enclose the whole pattern"
        );
    }

    static void AnalyzeDeclarationSemicolon(SyntaxNodeAnalysisContext context) {
        var (open, semicolon) = context.Node switch {
            TypeDeclarationSyntax type => (type.OpenBraceToken, type.SemicolonToken),
            EnumDeclarationSyntax declaration => (declaration.OpenBraceToken, declaration.SemicolonToken),
            NamespaceDeclarationSyntax declaration => (declaration.OpenBraceToken, declaration.SemicolonToken),
            _ => (default(SyntaxToken), default(SyntaxToken))
        };

        // ⚠ The braces are what make the semicolon optional. `record R(int X);` has no body and the
        // semicolon is the declaration's terminator, not decoration.
        if (!open.IsKind(SyntaxKind.OpenBraceToken)
            || open.IsMissing
            || !semicolon.IsKind(SyntaxKind.SemicolonToken)
            || semicolon.IsMissing) {
            return;
        }

        var member = (MemberDeclarationSyntax)context.Node;
        var intended = member switch {
            TypeDeclarationSyntax type => type.WithSemicolonToken(default),
            EnumDeclarationSyntax type => type.WithSemicolonToken(default),
            NamespaceDeclarationSyntax type => (MemberDeclarationSyntax)type.WithSemicolonToken(default),
            _ => member
        };
        Report(
            context,
            member,
            intended,
            semicolon.Span,
            string.Empty,
            "The declaration's trailing semicolon is redundant"
        );
    }

    static void AnalyzeElementBraces(SyntaxNodeAnalysisContext context) {
        var initializer = (InitializerExpressionSyntax)context.Node;
        if (initializer.Expressions.Count != 1
            || initializer.Parent is not InitializerExpressionSyntax {
                RawKind: (int)SyntaxKind.CollectionInitializerExpression
            }) {
            return;
        }

        // ⚠ An assignment is the one element the braces are load-bearing for (#412's audit, #424).
        // `{ x = 2 }` is a call to `Add(x = 2)`; bare, `x = 2` in a collection initializer is CS0747,
        // and as the *only* element the parser reads `{ Count = 7 }` as an object initializer that
        // sets a member — `new Bag { { Count = 7 } }` printed `0 1 7` and `new Bag { Count = 7 }`
        // prints `7 0 0`. ⚠ Both are declined by the re-parse below as well — measured by deleting this
        // guard, under which neither fixture fired, so the second does not "parse the same and fail
        // only in the binder" as this comment first guessed. The guard stays so the rule says why
        // rather than relying on the proof to notice.
        if (initializer.Expressions[0] is AssignmentExpressionSyntax) {
            return;
        }

        Report(
            context,
            initializer,
            initializer.Expressions[0],
            initializer.Span,
            initializer.Expressions[0].ToString(),
            "A one-argument collection element needs no braces"
        );
    }

    static void AnalyzeAnonymousMember(SyntaxNodeAnalysisContext context) {
        var member = (AnonymousObjectMemberDeclaratorSyntax)context.Node;
        if (member.NameEquals is not { } name) {
            return;
        }

        // The two spellings C# infers a name from. A generic name infers nothing, so `x.Foo<int>`
        // is left alone even where the written name matches.
        var inferred = member.Expression switch {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression, Name: IdentifierNameSyntax accessed
            } => accessed.Identifier.Text,
            _ => null
        };

        if (inferred is null || !string.Equals(inferred, name.Name.Identifier.Text, StringComparison.Ordinal)) {
            return;
        }

        Report(
            context,
            member,
            member.WithNameEquals(null),
            TextSpan.FromBounds(name.SpanStart, member.Expression.SpanStart),
            string.Empty,
            "The anonymous type's property name is the one the expression infers"
        );
    }

    static void AnalyzeOrdering(SyntaxNodeAnalysisContext context) {
        var ordering = (OrderingSyntax)context.Node;
        if (!ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.AscendingKeyword)) {
            return;
        }

        Report(
            context,
            ordering,
            ordering.WithAscendingOrDescendingKeyword(default),
            TextSpan.FromBounds(ordering.Expression.Span.End, ordering.Span.End),
            string.Empty,
            "`ascending` is the ordering a query already has"
        );
    }

    static void AnalyzeRange(SyntaxNodeAnalysisContext context) {
        var range = (RangeExpressionSyntax)context.Node;
        var edits = new List<(TextSpan Span, string Text)>(2);

        // ⚠ The literal `0`, read from the source rather than from a constant value: a named constant
        // that happens to be zero is a name the author chose, and deleting it deletes the name.
        if (range.LeftOperand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } left
            && left.Token.Text == "0") {
            edits.Add((left.Span, string.Empty));
        }

        if (range.RightOperand is PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.IndexExpression,
                Operand: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } end
            } right
            && end.Token.Text == "0") {
            edits.Add((right.Span, string.Empty));
        }

        if (edits.Count == 0 || RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(range.SyntaxTree, range.Span)) {
            return;
        }

        var intended = SyntaxFactory.RangeExpression(
            edits.Exists(edit => edit.Span == range.LeftOperand?.Span) ? null : range.LeftOperand,
            range.OperatorToken,
            edits.Exists(edit => edit.Span == range.RightOperand?.Span) ? null : range.RightOperand
        );
        if (!FixReparse.Preserves(range, intended, context.CancellationToken, edits.ToArray())) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                range.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "The range bound is the one the range already has"
            )
        );
    }

    static void AnalyzePropertyPattern(SyntaxNodeAnalysisContext context) {
        var pattern = (RecursivePatternSyntax)context.Node;

        // ⚠ The type is what makes the empty clause say nothing. `x is { }` on its own is a null
        // test, and `x is` is not a program.
        if (pattern.Type is not { } type
            || pattern.PositionalPatternClause is not null
            || pattern.PropertyPatternClause is not { Subpatterns.Count: 0 } clause) {
            return;
        }

        // ⚠ What is left is a bare type, and a bare type in a pattern is bound as an *expression*
        // first: `case Point { }:` is a type test and `case Point:` is the constant `Point` wherever
        // one is in scope (#424, the SK0250 shape). Directly under `is` it is the `is` operator and
        // binds the type; a predefined, array or nullable type cannot be captured by a constant. The
        // rest needs a lookup this syntax-only rule does not do, so it declines.
        if (pattern.Parent is not IsPatternExpressionSyntax && type is NameSyntax) {
            return;
        }

        Report(
            context,
            pattern,
            pattern,
            TextSpan.FromBounds(type.Span.End, clause.Span.End),
            string.Empty,
            "The property pattern under a type matches nothing the type test did not"
        );
    }

    /// <summary>
    ///     Reports the edit that turns <paramref name="original" /> into <paramref name="intended" />,
    ///     once a re-parse shows the text means that (#424).
    /// </summary>
    /// <remarks>
    ///     ⚠ Every branch here deletes punctuation, and punctuation is what the parser reads structure
    ///     from. Where <paramref name="intended" /> is <paramref name="original" /> itself, the shape
    ///     changes kind in a way only the parser decides — <c>x is T { }</c> becomes the <c>is</c>
    ///     operator — and the check is that nothing <em>around</em> it re-parses differently and no
    ///     token glued.
    /// </remarks>
    static void Report(
        SyntaxNodeAnalysisContext context,
        SyntaxNode original,
        SyntaxNode intended,
        TextSpan span,
        string replacement,
        string message
    ) {
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(context.Node.SyntaxTree, span)) {
            return;
        }

        var proved = original == intended
            ? FixReparse.Reparsed(original, [(span, replacement)], context.CancellationToken) is not null
            : FixReparse.Preserves(original, intended, context.CancellationToken, (span, replacement));
        if (!proved) {
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
