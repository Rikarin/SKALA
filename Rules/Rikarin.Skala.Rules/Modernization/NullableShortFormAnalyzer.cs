using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1040</c> — <c>Nullable&lt;T&gt;</c> written where <c>T?</c> is the same type.
/// </summary>
/// <remarks>
///     ⚠ The rewrite is only free where <c>T?</c> is <em>legal syntax in that position</em>, and the
///     positions where it is not are the whole rule. <c>typeof(Nullable&lt;&gt;)</c> has no short form
///     at all; a <c>cref</c> and a <c>nameof</c> argument both name the type rather than using it, so
///     <c>int?</c> either does not parse there or names something else; and a pattern is the one place
///     the compiler rejects a nullable value type outright. Each of those is a fix that would not
///     compile, which docs/plan/10 says is the worst thing a fixing tool can produce.
///     <para>
///         ⚠ The identifier <c>Nullable</c> is not enough. A user type called <c>Nullable&lt;T&gt;</c>
///         is legal and has no short form, so the symbol is bound and checked against
///         <see cref="SpecialType.System_Nullable_T" /> before anything is reported.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableShortFormAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NullableShortForm);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.GenericName);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var generic = (GenericNameSyntax)context.Node;
        if (!string.Equals(generic.Identifier.ValueText, "Nullable", System.StringComparison.Ordinal)
            || generic.TypeArgumentList.Arguments.Count != 1) {
            return;
        }

        var argument = generic.TypeArgumentList.Arguments[0];
        if (argument is OmittedTypeArgumentSyntax) {
            // `typeof(Nullable<>)`. There is no short form of an unbound generic type.
            return;
        }

        var node = Outermost(generic);
        if (!IsRewritablePosition(node)) {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol
            is not INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirective(node)) {
            return;
        }

        var replacement = node.SyntaxTree.GetText().ToString(argument.Span) + "?";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(node.SyntaxTree, node.Span),
                FixEdits.Pack((node.Span, replacement)),
                "Use `" + RewriteGuards.Trim(replacement) + "` instead of `" + RewriteGuards.Trim(node.ToString()) + "`"
            )
        );
    }

    /// <summary>
    ///     The whole type name, so that <c>System.Nullable&lt;int&gt;</c> is replaced and not only its
    ///     last segment.
    /// </summary>
    static TypeSyntax Outermost(TypeSyntax node) {
        while (true) {
            switch (node.Parent) {
                case QualifiedNameSyntax qualified when qualified.Right == node:
                    node = qualified;
                    continue;

                case AliasQualifiedNameSyntax alias when alias.Name == node:
                    node = alias;
                    continue;

                default:
                    return node;
            }
        }
    }

    /// <summary>
    ///     ⚠ Whether <c>T?</c> is legal where this <c>Nullable&lt;T&gt;</c> stands.
    /// </summary>
    /// <remarks>
    ///     Each exclusion is a place the compiler would reject the short form or read it as something
    ///     else, not a place the rewrite is merely unattractive: a <c>cref</c> and a <c>nameof</c>
    ///     argument name the type, a <c>using</c> alias and a member-access receiver require a plain
    ///     name, a pattern may not carry a nullable value type, and a pointer's element type may not
    ///     be spelled with <c>?</c>.
    /// </remarks>
    static bool IsRewritablePosition(TypeSyntax node) {
        if (node.Parent is MemberAccessExpressionSyntax access && access.Expression == node) {
            return false;
        }

        if (node.Parent is PointerTypeSyntax) {
            return false;
        }

        if (node.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } binary
            && binary.Right == node) {
            return false;
        }

        for (SyntaxNode? current = node; current is not null; current = current.Parent) {
            switch (current) {
                case CrefSyntax:
                case XmlNameAttributeSyntax:
                case UsingDirectiveSyntax:
                case PatternSyntax:
                    return false;

                case InvocationExpressionSyntax {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                } invocation when invocation.ArgumentList.Span.Contains(node.Span):
                    return false;

                case StatementSyntax:
                case MemberDeclarationSyntax:
                    return true;
            }
        }

        return true;
    }
}
