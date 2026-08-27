using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
/// <c>SK2013</c> — <c>new SomeException(…);</c> as a statement, with the <c>throw</c> missing.
/// </summary>
/// <remarks>
/// docs/plan/08-rule-catalogue.md § "SK2000 — Correctness". A guard clause that constructs an
/// exception and does not throw it is a guard clause that does nothing: the method continues down
/// the path the author meant to abort, and the failure surfaces later somewhere with no connection
/// to the cause.
/// <para>
/// ⚠ The narrowness is the rule. It fires only where the object creation <em>is</em> the whole of
/// an expression statement — not assigned, not returned, not an argument, not added to anything —
/// because in every one of those positions the exception object is used and the construction is
/// deliberate. Whatever the constructor's arguments do, an exception that reaches no one is a
/// statement with no effect a caller can observe.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedExceptionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ExceptionConstructedNotThrown);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var exception = start.Compilation.GetTypeByMetadataName("System.Exception");
                if (exception is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, exception),
                    SyntaxKind.ExpressionStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol exception) {
        var statement = (ExpressionStatementSyntax)context.Node;

        // ⚠ `ObjectCreationExpressionSyntax` and its implicitly-typed sibling both, because
        // `Exception e = new();` is not this pattern but `new();` as a statement would be — and a
        // target-typed `new` in statement position has no target, so it does not compile and never
        // reaches here. Handling both costs one line and means the rule does not quietly depend on
        // which spelling the author used.
        if (statement.Expression is not BaseObjectCreationExpressionSyntax creation) {
            return;
        }

        var created = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type;
        if (created is null || created.TypeKind == TypeKind.Error || !DerivesFromException(created, exception)) {
            return;
        }

        var fix = FixEdits.Pack((new TextSpan(creation.SpanStart, 0), "throw "));
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                statement.GetLocation(),
                fix,
                "`" + created.Name + "` is constructed and discarded; the `throw` is missing"
            )
        );
    }

    static bool DerivesFromException(ITypeSymbol type, INamedTypeSymbol exception) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, exception)) {
                return true;
            }
        }

        return false;
    }
}
