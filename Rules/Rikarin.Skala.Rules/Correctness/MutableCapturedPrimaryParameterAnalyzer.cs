using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2194</c> — a member body assigns a primary constructor parameter, which turns the hidden
///     capture field into mutable state with no declaration.
/// </summary>
/// <remarks>
///     ⚠ <b>Capture itself is the feature and is not reported.</b> A primary constructor parameter
///     read from a member body becomes a compiler-generated field, and that is the point of the
///     syntax — reporting it would be reporting C# 12. What the language gives no way to say is that
///     the field is <c>readonly</c>: the moment any body <em>writes</em> the parameter, the type has
///     mutable instance state that appears in no declaration, has no name a reader can search for,
///     carries no modifier, and is invisible to every rule that reasons about fields.
///     <para>
///         ⚠ <b><c>CS9107</c> was probed and covers a different overlap, and it is not
///         <c>CS9124</c>.</b> The compiler warns — always on, no analyzer package — when a captured
///         parameter's value is <em>also passed to the base constructor</em>, because the base may
///         capture it too. It says nothing about a parameter that is merely assigned. That case is
///         excluded here rather than reported twice.
///     </para>
///     <para>
///         ⚠ <b>Records are excluded, and the reason is the trap this repository has already paid
///         for.</b> In a positional record the parameter is also where the property is written down,
///         the two symbols point at the same <c>ParameterSyntax</c>, and a name in a member body
///         resolves to the property rather than to the capture. That is a different analysis with a
///         different answer, and guessing at it from the parameter's shape is how a rule ships dead.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableCapturedPrimaryParameterAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor =
        SkalaRule.Descriptor(RuleIds.MutableCapturedPrimaryParameter);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // ⚠ Class and struct declarations only. A record is `RecordDeclarationSyntax`, a different
        // node kind, so the exclusion is structural rather than a test somebody can forget.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (declaration.ParameterList is not { Parameters.Count: > 0 } parameters
            || !SkalaRule.MeetsLanguageVersion(context.Compilation, "12.0")) {
            return;
        }

        var passedToBase = BaseArguments(declaration);
        foreach (var parameter in parameters.Parameters) {
            if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol
                || passedToBase.Contains(parameter.Identifier.ValueText, System.StringComparer.Ordinal)
                || !IsAssignedInAMemberBody(declaration, symbol, context.SemanticModel, context.CancellationToken)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    parameter.GetLocation(),
                    "`"
                    + parameter.Identifier.ValueText
                    + "` is captured and assigned, so the type carries a mutable field that is declared nowhere"
                )
            );
        }
    }

    /// <summary>
    ///     ⚠ Matched on the identifier written in the base-constructor argument list, which is what
    ///     <c>CS9107</c> itself keys on. Anything more elaborate would start disagreeing with the
    ///     compiler about the one case it does report.
    /// </summary>
    static ImmutableHashSet<string> BaseArguments(TypeDeclarationSyntax declaration) =>
        declaration.BaseList?.Types
            .OfType<PrimaryConstructorBaseTypeSyntax>()
            .SelectMany(static baseType => baseType.ArgumentList.Arguments)
            .SelectMany(static argument => argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(static name => name.Identifier.ValueText)
            .ToImmutableHashSet(System.StringComparer.Ordinal)
        ?? ImmutableHashSet<string>.Empty;

    static bool IsAssignedInAMemberBody(
        TypeDeclarationSyntax declaration,
        IParameterSymbol parameter,
        SemanticModel model,
        CancellationToken cancellation
    ) =>
        declaration.Members
            .SelectMany(static member => member.DescendantNodes())
            .Any(node => IsAWrite(node, parameter, model, cancellation) && InAMemberBody(node));

    static bool IsAWrite(SyntaxNode node, IParameterSymbol parameter, SemanticModel model, CancellationToken token) {
        var written = node switch {
            AssignmentExpressionSyntax assignment => assignment.Left,
            PrefixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
            } prefix => prefix.Operand,
            PostfixUnaryExpressionSyntax {
                RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
            } postfix => postfix.Operand,
            ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword or (int)SyntaxKind.OutKeyword }
                argument => argument.Expression,
            _ => null
        };

        return written is IdentifierNameSyntax
            && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(written, token).Symbol, parameter);
    }

    /// <summary>
    ///     ⚠ A field or property <em>initializer</em> is not a member body and is excluded here.
    ///     Initializers run while the constructor is running, where the parameter is an ordinary
    ///     parameter and writing it captures nothing.
    /// </summary>
    static bool InAMemberBody(SyntaxNode node) {
        for (var current = node; current is not null; current = current.Parent) {
            switch (current) {
                case EqualsValueClauseSyntax when current.Parent is VariableDeclaratorSyntax {
                    Parent.Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax
                }:
                case EqualsValueClauseSyntax when current.Parent is PropertyDeclarationSyntax:
                case ConstructorInitializerSyntax:
                case BaseListSyntax:
                    return false;
                case BlockSyntax:
                case ArrowExpressionClauseSyntax:
                    return true;
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }
}
