using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1020</c> — a hand-written null guard that <c>ArgumentNullException.ThrowIfNull</c> is.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "Newer BCL over older idiom". ⚠ The rule is silent unless the
///     helper actually exists in the compilation: a project on an older target framework would
///     otherwise be handed a fix that does not compile, which is the failure mode
///     <c>languageVersion</c> exists to prevent one level up.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ThrowIfNullAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ArgumentNullThrowIfNull);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var exception = start.Compilation.GetTypeByMetadataName("System.ArgumentNullException");
                if (exception is null || !HasThrowIfNull(exception)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(context => Analyze(context, exception), SyntaxKind.IfStatement);
            }
        );
    }

    static bool HasThrowIfNull(INamedTypeSymbol exception) {
        foreach (var member in exception.GetMembers("ThrowIfNull")) {
            if (member is IMethodSymbol { IsStatic: true, Parameters.Length: >= 1 }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether this null comparison is the condition of a guard <c>SK1020</c> owns.
    /// </summary>
    /// <remarks>
    ///     ⚠ Called by <see cref="NullPatternAnalyzer" /> so that one line does not produce two findings
    ///     with two fixes, the second of which goes stale the moment the first is applied. Purely
    ///     syntactic on purpose: it is a question about who reports, not about what is true.
    /// </remarks>
    public static bool IsArgumentNullGuard(BinaryExpressionSyntax binary) =>
        binary.Parent is IfStatementSyntax statement
        && ReferenceEquals(statement.Condition, binary)
        && ThrownArgumentNullName(statement) is not null;

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol exception) {
        var statement = (IfStatementSyntax)context.Node;
        if (statement.Else is not null) {
            return;
        }

        var guarded = GuardedName(statement);
        if (guarded is null) {
            return;
        }

        var thrown = ThrownArgumentNullName(statement);
        if (thrown is null || thrown != guarded.Identifier.ValueText) {
            return;
        }

        var cancellation = context.CancellationToken;
        var model = context.SemanticModel;

        // The `== null` form only converts where SK1010's question has the same answer: a type with
        // a user-defined operator may not be null when `x == null` says it is.
        if (statement.Condition is BinaryExpressionSyntax binary) {
            var operand = NullComparison.OperandOf(binary);
            if (operand is null || !NullComparison.IsRewritable(model, operand, cancellation)) {
                return;
            }
        }

        // The thrown type has to be `System.ArgumentNullException` itself, not something a
        // repository declared with the same name and different behaviour.
        if (statement.Statement is not StatementSyntax body) {
            return;
        }

        var throwStatement = SingleThrow(body);
        if (throwStatement?.Expression is not ObjectCreationExpressionSyntax creation) {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(
                model.GetSymbolInfo(creation.Type, cancellation).Symbol as INamedTypeSymbol,
                exception
            )) {
            return;
        }

        if (statement.ContainsDirectives) {
            return;
        }

        var replacement = "ArgumentNullException.ThrowIfNull(" + guarded.Identifier.ValueText + ");";
        var fix = FixEdits.Pack((statement.Span, replacement));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, statement.Span),
                fix,
                "Use ArgumentNullException.ThrowIfNull(" + guarded.Identifier.ValueText + ")"
            )
        );
    }

    /// <summary>The identifier a guard tests, for both <c>x is null</c> and <c>x == null</c>.</summary>
    static IdentifierNameSyntax? GuardedName(IfStatementSyntax statement) =>
        statement.Condition switch {
            IsPatternExpressionSyntax {
                Expression: IdentifierNameSyntax name,
                Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal }
            } when literal.IsKind(SyntaxKind.NullLiteralExpression) => name,
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.EqualsExpression) =>
                NullComparison.OperandOf(binary) as IdentifierNameSyntax,
            _ => null
        };

    /// <summary>
    ///     The <c>nameof(x)</c> argument of a lone <c>throw new ArgumentNullException(nameof(x))</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Exactly one argument, and it has to be a <c>nameof</c>. A guard that passes a message, or
    ///     throws a derived exception, or logs beside the throw, is a deliberate choice; replacing it
    ///     with the helper would delete the part someone wrote on purpose.
    /// </remarks>
    static string? ThrownArgumentNullName(IfStatementSyntax statement) {
        if (statement.Else is not null || statement.Statement is null) {
            return null;
        }

        var throwStatement = SingleThrow(statement.Statement);
        if (throwStatement?.Expression is not ObjectCreationExpressionSyntax creation) {
            return null;
        }

        if (creation.Type is not (IdentifierNameSyntax or QualifiedNameSyntax)
            || !TypeNameIs(creation.Type, "ArgumentNullException")) {
            return null;
        }

        if (creation.Initializer is not null || creation.ArgumentList is not { Arguments.Count: 1 } arguments) {
            return null;
        }

        return arguments.Arguments[0].Expression is InvocationExpressionSyntax {
            Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" },
            ArgumentList.Arguments.Count: 1
        } nameOf
            && nameOf.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax named
                ? named.Identifier.ValueText
                : null;
    }

    static bool TypeNameIs(TypeSyntax type, string name) =>
        type switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == name,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == name,
            _ => false
        };

    static ThrowStatementSyntax? SingleThrow(StatementSyntax body) =>
        body switch {
            ThrowStatementSyntax single => single,
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ThrowStatementSyntax,
            _ => null
        };
}
