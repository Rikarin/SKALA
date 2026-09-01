using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3532</c> — a <c>ref struct</c> constructs a disposable <c>ref struct</c> and declares no
///     way to release it.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime". ⚠
///     <b>
///         This is the one
///         place in the disposal family that nothing else can reach.
///     </b> A <c>ref struct</c> that offers
///     only a public parameterless <c>Dispose()</c> is disposable through the pattern rule and through
///     nothing else — it implements no interface, so <c>SK3502</c>, which asks whether the owner
///     implements the contract the field offers, has no contract to ask about and is silent by
///     construction. The resource is owned, never released, and no analyzer in the family says so.
///     <para>
///         ⚠
///         <b>
///             The field's type must implement neither <c>IDisposable</c> nor <c>IAsyncDisposable</c>,
///             and that is the disjointness guard rather than a limitation.
///         </b> C# 13 lets a <c>ref struct</c>
///         implement an interface, so without the test the two rules would both report one field — and
///         <c>supersedes</c> is the wrong instrument, because <c>Supersession.Apply</c> suppresses the
///         superseded finding rather than the duplicate one.
///     </para>
///     <para>
///         ⚠ <b>No fix.</b> A correct <c>Dispose()</c> here decides which fields it releases, in what
///         order, and whether a second call is safe; and the repair may instead be that this type should
///         not own the resource at all. No edit answers that.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RefStructOwnedDisposableAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.RefStructOwnsUndisposedResource);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var disposable = start.Compilation.GetTypeByMetadataName("System.IDisposable");
                var asyncDisposable = start.Compilation.GetTypeByMetadataName("System.IAsyncDisposable");
                if (disposable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, disposable, asyncDisposable),
                    SyntaxKind.StructDeclaration
                );
            }
        );
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol disposable,
        INamedTypeSymbol? asyncDisposable
    ) {
        var declaration = (StructDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            is not INamedTypeSymbol owner
            || !owner.IsRefLikeType
            // ⚠ Another part of a partial may hold the `Dispose`, and it is not in this tree.
            || owner.DeclaringSyntaxReferences.Length != 1
            || HasPatternDispose(owner)) {
            return;
        }

        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>()) {
            if (field.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))) {
                continue;
            }

            foreach (var variable in field.Declaration.Variables) {
                if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
                    is not IFieldSymbol symbol
                    || symbol.Type.TypeKind == TypeKind.Error
                    || !symbol.Type.IsRefLikeType
                    || !HasPatternDispose(symbol.Type)
                    // ⚠ The disjointness guard. A `ref struct` may implement `IDisposable` from C# 13
                    // on, and where it does the ownership is one `SK3502` can see and report.
                    || UsingResource.Implements(symbol.Type, disposable)
                    || UsingResource.Implements(symbol.Type, asyncDisposable)
                    || !IsConstructedHere(context, declaration, variable, symbol)) {
                    continue;
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        variable.Identifier.GetLocation(),
                        "`"
                        + owner.Name
                        + "` constructs `"
                        + symbol.Type.Name
                        + "`, which is disposable only through the pattern, and declares no `Dispose()`"
                    )
                );
            }
        }
    }

    /// <summary>
    ///     The contract a <c>using</c> binds to when there is no interface: <c>public void Dispose()</c>.
    /// </summary>
    static bool HasPatternDispose(ITypeSymbol type) {
        foreach (var member in type.GetMembers("Dispose")) {
            if (member is IMethodSymbol {
                    ReturnsVoid: true,
                    Parameters.IsEmpty: true,
                    IsStatic: false,
                    DeclaredAccessibility: Accessibility.Public
                }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether this type builds the resource rather than borrowing one it was handed.
    /// </summary>
    /// <remarks>
    ///     ⚠ The same ownership proof <c>SK3502</c> uses — a direct object creation — read from the two
    ///     places a <c>ref struct</c> can put one. A field assigned from a constructor parameter is a
    ///     borrowed resource whose lifetime belongs to the caller, and disposing it here would close
    ///     something somebody else is still holding.
    /// </remarks>
    static bool IsConstructedHere(
        SyntaxNodeAnalysisContext context,
        StructDeclarationSyntax declaration,
        VariableDeclaratorSyntax variable,
        IFieldSymbol field
    ) {
        if (variable.Initializer?.Value is BaseObjectCreationExpressionSyntax) {
            return true;
        }

        foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>()) {
            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                || assignment.Right is not BaseObjectCreationExpressionSyntax
                || !Names(assignment.Left, field, context.SemanticModel, context.CancellationToken)) {
                continue;
            }

            return true;
        }

        return false;
    }

    static bool Names(
        ExpressionSyntax target,
        IFieldSymbol field,
        SemanticModel model,
        CancellationToken cancellation
    ) {
        var expression = UsingResource.Unwrap(target);
        if (expression is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access) {
            expression = access.Name;
        }

        return expression is SimpleNameSyntax
            && SymbolEqualityComparer.Default.Equals(
                model.GetSymbolInfo(expression, cancellation).Symbol,
                field
            );
    }
}
