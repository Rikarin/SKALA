using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
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
        context.RegisterOperationAction(Analyze, OperationKind.FieldInitializer);
    }

    /// <summary>
    ///     One field initializer, against every constructor of its type that runs it.
    /// </summary>
    /// <remarks>
    ///     ⚠ An operation action rather than the symbol action it was until #423, because whether the
    ///     initializer may be deleted is now a semantic question — <see cref="RewriteGuards.IsFreeToSkip(IOperation)" />
    ///     on the value — and a symbol action has no model of its own (RS1030).
    /// </remarks>
    static void Analyze(OperationAnalysisContext context) {
        var initializer = (IFieldInitializerOperation)context.Operation;
        if (initializer.InitializedFields.Length != 1
            || initializer.InitializedFields[0] is not {
                IsStatic: false,
                IsConst: false,
                IsImplicitlyDeclared: false,
                DeclaredAccessibility: Accessibility.Private
            } field) {
            return;
        }

        var type = field.ContainingType;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsRecord) {
            return;
        }

        // ⚠ The fix deletes the initializer, so its evaluation must be free to skip: no getter, no
        // user-defined conversion or operator, and no throw (#423). `IsSideEffectFree`, the syntactic
        // test this replaced, admitted `Counter.Next` and `(W)4` as a name path and a cast, and the
        // audit measured both running before the fix and not after.
        if (!RewriteGuards.IsFreeToSkip(initializer.Value)
            || RunningConstructors(type, context.CancellationToken) is not { Count: > 0 } running) {
            return;
        }

        Examine(context, type, field, running);
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
        OperationAnalysisContext context,
        INamedTypeSymbol type,
        IFieldSymbol field,
        List<ConstructorDeclarationSyntax> running
    ) {
        if (field.DeclaringSyntaxReferences.Length != 1
            || field.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
            is not VariableDeclaratorSyntax { Initializer: { } initializer } declarator) {
            return;
        }

        if (ReferencedInAnOverride(type, field, context.CancellationToken)) {
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
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression, Expression: ThisExpressionSyntax
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
}
