using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1061</c> — a name computed at run time, or written as a literal where an identifier belongs.
/// </summary>
/// <remarks>
///     ⚠ <b>The hard half of this rule is everything it refuses.</b> A serialization key, a JSON
///     property name, a SQL column and an <c>.editorconfig</c> key are all string literals that look
///     exactly like an identifier and mean something a rename must not follow. Nothing in the syntax
///     separates them from a name that was meant as a name, so the literal shapes fire only where the
///     *position* defines the meaning — a <c>paramName</c> parameter, and the
///     <c>PropertyChangedEventArgs</c> family — and never on a bare literal that happens to match a
///     member.
///     <para>
///         ⚠ The two computed shapes each carry a soundness condition that a pattern match would miss:
///         <c>typeof(T).Name</c> is <c>nameof(T)</c> only for a non-generic named type spelled by its
///         own name, and an enum member's <c>ToString()</c> is its name only where no other member
///         shares its value.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NameofExpressionAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.NameofExpression);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.NameofExpression);

    static readonly ImmutableHashSet<string> PropertyEventArgs = ImmutableHashSet.Create(
        System.StringComparer.Ordinal,
        "System.ComponentModel.PropertyChangedEventArgs",
        "System.ComponentModel.PropertyChangingEventArgs"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(AnalyzeTypeName, SyntaxKind.SimpleMemberAccessExpression);
                start.RegisterSyntaxNodeAction(AnalyzeEnumToString, SyntaxKind.InvocationExpression);
                start.RegisterSyntaxNodeAction(
                    AnalyzeIdentifierPosition,
                    SyntaxKind.InvocationExpression,
                    SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression
                );
            }
        );
    }

    /// <summary>Shape 1: <c>typeof(Widget).Name</c> is <c>nameof(Widget)</c>.</summary>
    static void AnalyzeTypeName(SyntaxNodeAnalysisContext context) {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name.Identifier.ValueText != "Name"
            || access.Expression is not TypeOfExpressionSyntax typeOf) {
            return;
        }

        // ⚠ The written spelling has to be a plain name. `typeof(int).Name` is "Int32" and
        // `nameof(int)` does not compile; an array, a nullable spelling or a generic name all
        // produce a metadata name that `nameof` does not.
        if (typeOf.Type is not (IdentifierNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax)) {
            return;
        }

        foreach (var node in typeOf.Type.DescendantNodesAndSelf()) {
            if (node is GenericNameSyntax) {
                return;
            }
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(typeOf.Type, cancellation).Type is not INamedTypeSymbol {
                Arity: 0,
                TypeKind: not (TypeKind.Error or TypeKind.Dynamic or TypeKind.TypeParameter)
            } type) {
            return;
        }

        // ⚠ The alias guard. `using Text = System.String;` makes `nameof(Text)` produce "Text" and
        // `typeof(Text).Name` produce "String" — the same expression, two different answers.
        if (LastIdentifier(typeOf.Type) is not { } written
            || !string.Equals(written, type.Name, System.StringComparison.Ordinal)) {
            return;
        }

        if (NullComparison.InsideExpressionTree(model, access, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(access)) {
            return;
        }

        Report(context, access.Span, "nameof(" + typeOf.Type + ")", "The type's name is known at compile time");
    }

    /// <summary>Shape 2: <c>Colour.Red.ToString()</c> is <c>nameof(Colour.Red)</c>.</summary>
    static void AnalyzeEnumToString(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0
            || invocation.Expression is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression
            } call
            || call.Name.Identifier.ValueText != "ToString"
            || call.Expression is not (MemberAccessExpressionSyntax or IdentifierNameSyntax)) {
            return;
        }

        var member = call.Expression;

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(member, cancellation).Symbol is not IFieldSymbol {
                IsConst: true,
                ContainingType: { TypeKind: TypeKind.Enum } declaring
            } field
            || field.ConstantValue is null) {
            return;
        }

        // ⚠ `Enum.ToString()` returns the name of the *first* member declared with the value, not the
        // name that was written. `enum E { A = 1, B = 1 }` makes `E.B.ToString()` return "A", and a
        // rewrite to `nameof(E.B)` would change what the program prints.
        var duplicates = 0;
        foreach (var candidate in declaring.GetMembers()) {
            if (candidate is IFieldSymbol { IsConst: true, ConstantValue: { } value }
                && value.Equals(field.ConstantValue)) {
                duplicates++;
            }
        }

        if (duplicates != 1
            || NullComparison.InsideExpressionTree(model, invocation, cancellation)
            || RewriteGuards.ContainsCommentOrDirective(invocation)) {
            return;
        }

        Report(
            context,
            invocation.Span,
            "nameof(" + member + ")",
            "The enum member's name is known at compile time"
        );
    }

    /// <summary>
    ///     Shapes 3 and 4: a string literal in a position defined to hold an identifier.
    /// </summary>
    static void AnalyzeIdentifierPosition(SyntaxNodeAnalysisContext context) {
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(context.Node, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        var arguments = context.Node switch {
            InvocationExpressionSyntax invocation => invocation.ArgumentList,
            BaseObjectCreationExpressionSyntax creation => creation.ArgumentList,
            _ => null
        };

        if (arguments is null || NullComparison.InsideExpressionTree(model, context.Node, cancellation)) {
            return;
        }

        var eventArgs = method.MethodKind == MethodKind.Constructor
            && PropertyEventArgs.Contains(method.ContainingType.ToDisplayString());

        for (var i = 0; i < arguments.Arguments.Count; i++) {
            var argument = arguments.Arguments[i];
            var parameter = argument.NameColon?.Name.Identifier.ValueText
                ?? (i < method.Parameters.Length ? method.Parameters[i].Name : null);

            // ⚠ `paramName` is the whole of the position test for the exception family. Nothing about
            // the *value* is consulted: a literal that merely resembles an identifier elsewhere in the
            // same call is a serialization key as far as this rule is concerned.
            var wanted = parameter == "paramName" || (eventArgs && parameter == "propertyName");
            if (!wanted
                || argument.Expression is not LiteralExpressionSyntax {
                    RawKind: (int)SyntaxKind.StringLiteralExpression
                } literal) {
                continue;
            }

            var name = literal.Token.ValueText;
            if (name.Length == 0
                || !SyntaxFacts.IsValidIdentifier(name)
                || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
                || RewriteGuards.ContainsCommentOrDirective(argument)) {
                continue;
            }

            var target = parameter == "paramName" ? Parameter(model, literal, name, cancellation) : null;
            if (parameter == "paramName" && target is null) {
                continue;
            }

            if (parameter == "propertyName" && !DeclaresProperty(model, literal, name, cancellation)) {
                continue;
            }

            Report(
                context,
                literal.Span,
                "nameof(" + name + ")",
                parameter == "paramName"
                    ? "The literal names a parameter in scope"
                    : "The literal names a property of this type"
            );
        }
    }

    static void Report(
        SyntaxNodeAnalysisContext context,
        Microsoft.CodeAnalysis.Text.TextSpan span,
        string replacement,
        string reason
    ) =>
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(context.Node.SyntaxTree, span),
                FixEdits.Pack((span, replacement)),
                reason + ": `" + RewriteGuards.Trim(replacement) + "`"
            )
        );

    /// <summary>A parameter of that name visible at this position, or null.</summary>
    /// <remarks>
    ///     ⚠ Positional, so it answers "would <c>nameof</c> written here bind to a parameter" rather
    ///     than "does some method somewhere take one". A property setter's <c>value</c> is a parameter
    ///     and is found by the same lookup, which is exactly right — <c>nameof(value)</c> compiles
    ///     there.
    /// </remarks>
    static IParameterSymbol? Parameter(
        SemanticModel model,
        SyntaxNode position,
        string name,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();
        foreach (var symbol in model.LookupSymbols(position.SpanStart, name: name)) {
            if (symbol is IParameterSymbol parameter) {
                return parameter;
            }
        }

        return null;
    }

    static bool DeclaresProperty(
        SemanticModel model,
        SyntaxNode position,
        string name,
        CancellationToken cancellation
    ) {
        cancellation.ThrowIfCancellationRequested();
        foreach (var symbol in model.LookupSymbols(position.SpanStart, name: name)) {
            if (symbol is IPropertySymbol) {
                return true;
            }
        }

        return false;
    }

    static string? LastIdentifier(TypeSyntax type) =>
        type switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => LastIdentifier(qualified.Right),
            AliasQualifiedNameSyntax aliased => LastIdentifier(aliased.Name),
            _ => null
        };
}
