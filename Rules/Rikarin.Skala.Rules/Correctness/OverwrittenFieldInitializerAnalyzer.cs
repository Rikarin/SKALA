using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2200</c> — a field initializer no allocation ever keeps.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK2200 — events, delegates and effects that do not happen".
///     Two values are written down for one field and only one of them is ever true.
///     <para>
///         ⚠ The subtle guard is the <c>override</c> one. Field initializers run <em>before</em> the base
///         constructor call, so a base constructor calling a virtual method this type overrides can
///         observe the initialized value — and in that program the initializer is not dead. Everything
///         else the rule checks is about the constructor's own statements; this one is about a call the
///         constructor does not contain.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverwrittenFieldInitializerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OverwrittenFieldInitializer);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    static void Analyze(SymbolAnalysisContext context) {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsRecord) {
            return;
        }

        if (RunningConstructors(type, context.CancellationToken) is not { Count: > 0 } running) {
            return;
        }

        foreach (var member in type.GetMembers()) {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (member is IFieldSymbol {
                    IsStatic: false,
                    IsConst: false,
                    IsImplicitlyDeclared: false,
                    DeclaredAccessibility: Accessibility.Private
                } field) {
                Examine(context, type, field, running);
            }
        }
    }

    /// <summary>
    ///     The declared constructors that <em>run</em> field initializers, or <c>null</c> for a type
    ///     whose constructors the walk cannot read.
    /// </summary>
    /// <remarks>
    ///     ⚠ C# runs field initializers in every constructor that does not chain to <c>this(…)</c>, so a
    ///     chaining constructor is evidence of nothing and is skipped rather than counted as a witness
    ///     or as a counterexample. A primary constructor, a record's copy constructor and any other
    ///     implicitly declared one stop the walk for the whole type: their assignments are not
    ///     constructor statements, and guessing at them is how this rule would delete a live value.
    /// </remarks>
    static List<ConstructorDeclarationSyntax>? RunningConstructors(
        INamedTypeSymbol type,
        CancellationToken cancellation
    ) {
        var running = new List<ConstructorDeclarationSyntax>();
        foreach (var constructor in type.InstanceConstructors) {
            if (constructor.IsImplicitlyDeclared || constructor.DeclaringSyntaxReferences.Length != 1) {
                return null;
            }

            if (constructor.DeclaringSyntaxReferences[0].GetSyntax(cancellation)
                is not ConstructorDeclarationSyntax declaration) {
                return null;
            }

            if (!declaration.Initializer.IsKind(SyntaxKind.ThisConstructorInitializer)) {
                running.Add(declaration);
            }
        }

        return running;
    }

    /// <summary>One private instance field, against every constructor that runs its initializer.</summary>
    static void Examine(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        IFieldSymbol field,
        List<ConstructorDeclarationSyntax> running
    ) {
        if (field.DeclaringSyntaxReferences.Length != 1
            || field.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
            is not VariableDeclaratorSyntax { Initializer: { } initializer } declarator) {
            return;
        }

        if (!IsSideEffectFree(initializer.Value)
            || ReferencedInAnOverride(type, field, context.CancellationToken)) {
            return;
        }

        foreach (var constructor in running) {
            if (!OverwritesBeforeAnyRead(constructor, field.Name)) {
                return;
            }
        }

        var span = TextSpan.FromBounds(declarator.Identifier.Span.End, initializer.Span.End);
        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(declarator.SyntaxTree, span)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                initializer.GetLocation(),
                FixEdits.Pack((span, string.Empty)),
                "the value given to `" + field.Name + "` here is overwritten by every constructor"
            )
        );
    }

    /// <summary>
    ///     Whether the constructor's first contact with the field is an unconditional overwrite.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every statement before the assignment has to be provably harmless, not merely free of the
    ///     field's name. An invocation, an object creation, `this` or `base` can each reach the field
    ///     without spelling it — and if anything reads the initialized value before it is replaced,
    ///     that value is observable and the initializer is not dead.
    /// </remarks>
    static bool OverwritesBeforeAnyRead(ConstructorDeclarationSyntax constructor, string name) {
        if (constructor.Initializer is { } initializer && Mentions(initializer, name)) {
            return false;
        }

        foreach (var statement in Statements(constructor)) {
            if (statement is ExpressionStatementSyntax {
                    Expression:
                    AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
                }
                && IsFieldTarget(assignment.Left, name)) {
                return !Mentions(assignment.Right, name);
            }

            if (Mentions(statement, name) || Reaches(statement)) {
                return false;
            }
        }

        return false;
    }

    static IEnumerable<StatementSyntax> Statements(ConstructorDeclarationSyntax constructor) {
        if (constructor.Body is { } body) {
            return body.Statements;
        }

        return constructor.ExpressionBody is { } arrow
            ? new StatementSyntax[] { SyntaxFactory.ExpressionStatement(arrow.Expression) }
            : System.Array.Empty<StatementSyntax>();
    }

    static bool IsFieldTarget(ExpressionSyntax left, string name) =>
        left switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: ThisExpressionSyntax
            } access => access.Name.Identifier.ValueText == name,
            _ => false
        };

    static bool Mentions(SyntaxNode node, string name) {
        foreach (var identifier in node.DescendantNodesAndSelf()) {
            if (identifier is IdentifierNameSyntax candidate && candidate.Identifier.ValueText == name) {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a statement could reach the instance without naming the field.</summary>
    static bool Reaches(SyntaxNode node) {
        foreach (var descendant in node.DescendantNodesAndSelf()) {
            if (descendant is InvocationExpressionSyntax
                or BaseObjectCreationExpressionSyntax
                or ThisExpressionSyntax
                or BaseExpressionSyntax
                or AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Whether the field is named inside a member this type overrides.
    /// </summary>
    /// <remarks>
    ///     A base constructor may call a virtual member, and it does so <em>after</em> this type's field
    ///     initializers have run and <em>before</em> this type's constructor body. An override reading
    ///     the field therefore sees the initialized value, which makes the initializer observable and
    ///     the finding wrong. The test is deliberately loose — any mention, read or write — because
    ///     every shape it recognises produces silence and none of them can produce a finding.
    /// </remarks>
    static bool ReferencedInAnOverride(INamedTypeSymbol type, IFieldSymbol field, CancellationToken cancellation) {
        foreach (var member in type.GetMembers()) {
            if (!member.IsOverride) {
                continue;
            }

            foreach (var reference in member.DeclaringSyntaxReferences) {
                if (Mentions(reference.GetSyntax(cancellation), field.Name)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ The forms the fix may delete. The fix removes the initializer, so anything that could do
    ///     work while producing its value is declined rather than reported.
    /// </summary>
    static bool IsSideEffectFree(ExpressionSyntax expression) {
        switch (expression) {
            case LiteralExpressionSyntax:
            case DefaultExpressionSyntax:
            case TypeOfExpressionSyntax:
            case SizeOfExpressionSyntax:
            case IdentifierNameSyntax:
            case PredefinedTypeSyntax:
            case ThisExpressionSyntax:
                return true;

            case ParenthesizedExpressionSyntax parenthesized:
                return IsSideEffectFree(parenthesized.Expression);

            case CastExpressionSyntax cast:
                return IsSideEffectFree(cast.Expression);

            case PrefixUnaryExpressionSyntax {
                RawKind:
                (int)SyntaxKind.UnaryMinusExpression
                    or (int)SyntaxKind.UnaryPlusExpression
                    or (int)SyntaxKind.BitwiseNotExpression
                    or (int)SyntaxKind.LogicalNotExpression
            } unary:
                return IsSideEffectFree(unary.Operand);

            case MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access:
                return RewriteGuards.IsPlainNamePath(access);

            // ⚠ `nameof` is an invocation in the tree and a constant in the language. Admitting it by
            // name is the one exception to "no invocations", and it is safe because the compiler
            // refuses any `nameof` argument that is not a name.
            case InvocationExpressionSyntax {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" },
                ArgumentList.Arguments.Count: 1
            }:
                return true;

            default:
                return false;
        }
    }
}
