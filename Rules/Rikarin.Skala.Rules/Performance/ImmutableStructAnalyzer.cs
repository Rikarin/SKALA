using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4022</c> — a struct that already satisfies <c>readonly struct</c> and does not declare it.
/// </summary>
/// <remarks>
///     ⚠ The predicate is the compiler's own rule for the modifier rather than an approximation of it,
///     which is what makes the fix safe: every instance field already <c>readonly</c>, no settable
///     instance property or indexer, and no member that writes <c>this</c>. If those hold the keyword
///     compiles, and if the keyword compiles the defensive copies at every <c>in</c> parameter and
///     every <c>readonly</c> field are gone.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImmutableStructAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ImmutableStructNotReadonly);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ImmutableStructNotReadonly);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        var registrar = PartialConstructorDefinitions.Visiting(context);
        registrar.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StructDeclaration);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (StructDeclarationSyntax)context.Node;
        var cancellation = context.CancellationToken;
        if (declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || declaration.Modifiers.Any(SyntaxKind.RefKeyword)
            || declaration.Modifiers.Any(SyntaxKind.UnsafeKeyword)
            || context.SemanticModel.GetDeclaredSymbol(declaration, cancellation) is not {
                DeclaringSyntaxReferences.Length: 1
            } symbol
            || !declaration.Members.All(Eligible)
            || Owned(declaration).Any(Unsafe)
            || Owned(declaration).OfType<ThisExpressionSyntax>().Any(Written)
            || WritesAPrimaryConstructorParameter(context.SemanticModel, declaration, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                FixEdits.Pack((new TextSpan(declaration.Keyword.SpanStart, 0), "readonly ")),
                "`" + symbol.Name + "` is never mutated; mark it `readonly struct` to remove the defensive copies"
            )
        );
    }

    /// <summary>
    ///     ⚠ Only the struct's own syntax. A nested type declares its own <c>this</c>, and searching
    ///     through one would judge a different instance's mutations as this struct's.
    /// </summary>
    static IEnumerable<SyntaxNode> Owned(StructDeclarationSyntax declaration) =>
        declaration.DescendantNodes(node => node == declaration || node is not TypeDeclarationSyntax);

    static bool Eligible(MemberDeclarationSyntax member) {
        if (member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) && member is not FieldDeclarationSyntax) {
            // A `readonly` member inside a `readonly struct` is redundant at best; leave the author to it.
            return false;
        }

        if (member.Modifiers.Any(SyntaxKind.StaticKeyword) || member is TypeDeclarationSyntax) {
            return true;
        }

        return member switch {
            // ⚠ A `fixed` buffer's declarator carries a size, and its storage cannot be readonly.
            FieldDeclarationSyntax field => (field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
                || field.Modifiers.Any(SyntaxKind.ConstKeyword))
                && field.Declaration.Variables.All(static variable => variable.ArgumentList is null),

            // ⚠ A field-like event's backing delegate field is written by its own accessors.
            EventFieldDeclarationSyntax => false,
            PropertyDeclarationSyntax property => !HasSetter(property.AccessorList),
            IndexerDeclarationSyntax indexer => !HasSetter(indexer.AccessorList),
            _ => true
        };
    }

    static bool HasSetter(AccessorListSyntax? accessors) =>
        accessors?.Accessors.Any(static accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)) == true;

    static bool Unsafe(SyntaxNode node) =>
        node is PointerTypeSyntax
            or FunctionPointerTypeSyntax
            or FixedStatementSyntax
        || node.IsKind(SyntaxKind.AddressOfExpression);

    /// <summary>
    ///     ⚠ A captured primary-constructor parameter is a field the predicate could not see.
    /// </summary>
    /// <remarks>
    ///     <c>struct Budget(decimal total) { void Spend(decimal a) => total -= a; }</c> passed every
    ///     check above — it declares no field at all — and <c>readonly struct Budget</c> is
    ///     <c>CS9114</c>, because a <c>readonly</c> type's captured parameters are <c>readonly</c> too.
    ///     Found by applying every safe fix to every fixture: the shape sat in an <c>SK2194</c> positive.
    ///     A parameter that is only read stays eligible. ⚠ Writing through a member of the parameter —
    ///     <c>total.Field = 1</c> — is counted as writing it, which is exact for a struct-typed parameter
    ///     and needlessly cautious for a class-typed one; the rule declines rather than asks which.
    /// </remarks>
    static bool WritesAPrimaryConstructorParameter(
        SemanticModel model,
        StructDeclarationSyntax declaration,
        CancellationToken cancellation
    ) {
        if (declaration.ParameterList is not { Parameters.Count: > 0 } parameters) {
            return false;
        }

        var names = new HashSet<string>(
            parameters.Parameters.Select(static parameter => parameter.Identifier.ValueText),
            StringComparer.Ordinal
        );

        foreach (var identifier in Owned(declaration).OfType<IdentifierNameSyntax>()) {
            if (names.Contains(identifier.Identifier.ValueText)
                && Written(identifier)
                && model.GetSymbolInfo(identifier, cancellation).Symbol is IParameterSymbol parameter
                && parameter.DeclaringSyntaxReferences.Any(reference => parameters.Span.Contains(reference.Span))) {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    ///     A parameter reference that is written, through any member access, parenthesis or tuple
    ///     element between it and the write.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>this</c> is deliberately not climbed the same way: <c>this.total = value</c> in a
    ///     constructor is legal in a <c>readonly struct</c>, and treating it as a write would decline
    ///     every struct whose constructor spells <c>this.</c>.
    /// </remarks>
    static bool Written(IdentifierNameSyntax reference) {
        SyntaxNode node = reference;
        while (node.Parent is MemberAccessExpressionSyntax access
               && access.Expression == node
               || node.Parent is ParenthesizedExpressionSyntax or TupleExpressionSyntax
               || node.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax }) {
            node = node.Parent;
        }

        return IsWriteTarget(node);
    }

    /// <summary>
    ///     <c>this = default</c>, <c>this++</c> and <c>Swap(ref this)</c> all compile in a struct that is
    ///     not <c>readonly</c> and in none that is.
    /// </summary>
    static bool Written(ThisExpressionSyntax expression) => IsWriteTarget(expression);

    static bool IsWriteTarget(SyntaxNode node) =>
        node.Parent switch {
            AssignmentExpressionSyntax assignment => assignment.Left == node,
            RefExpressionSyntax => true,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword),
            _ => false
        };
}
