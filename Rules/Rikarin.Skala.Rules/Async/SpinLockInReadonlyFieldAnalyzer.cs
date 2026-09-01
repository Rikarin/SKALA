using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Async;

/// <summary>
///     <c>SK3021</c> — a <c>SpinLock</c> is stored in a <c>readonly</c> field.
/// </summary>
/// <remarks>
///     docs/plan/08-rule-catalogue.md § "SK3000 — Async, concurrency, lifetime".
///     <c>SpinLock</c> is a mutable struct and its state <em>is</em> the lock. Reading a
///     <c>readonly</c> field to call <c>Enter</c> — which is not a <c>readonly</c> member — makes the
///     compiler take a defensive copy, so the lock is acquired on a temporary and released on a
///     temporary and nothing is ever excluded. It is a lock that compiles, runs, reports
///     <c>lockTaken: true</c> every time, and does not lock; the race it was written to prevent is
///     unchanged and the code above it says otherwise.
///     <para>
///         ⚠ Removing <c>readonly</c> is the only repair and it is not a cosmetic one: the field
///         becomes assignable and every call site starts operating on the real lock, which is a change
///         in what the program does. <c>fixIsSafe</c> is false for exactly that reason.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpinLockInReadonlyFieldAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SpinLockInReadonlyField);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (start.Compilation.GetTypeByMetadataName("System.Threading.SpinLock") is not { } spinLock) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, spinLock),
                    SyntaxKind.FieldDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol spinLock) {
        var field = (FieldDeclarationSyntax)context.Node;
        var keyword = field.Modifiers.FirstOrDefault(static modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));
        if (keyword.RawKind == (int)SyntaxKind.None || field.Declaration.Variables.Count == 0) {
            return;
        }

        // ⚠ The type is resolved, never matched on the written name. `SpinLock` is a plausible name
        // for somebody's own type, and removing `readonly` from a field of one would be a change
        // made for no reason at all.
        if (!SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetTypeInfo(field.Declaration.Type, context.CancellationToken).Type,
                spinLock
            )) {
            return;
        }

        // ⚠ Inside a `readonly struct` the keyword is not the author's choice: CS8340 requires every
        // instance field of one to be `readonly`, so deleting it produces a fix that does not
        // compile. The lock is just as broken there — the copy happens with or without the keyword —
        // but the repair is to stop the containing type being a `readonly struct`, which is a design
        // change and not an edit. Withheld rather than reported with an illegal fix.
        if (context.SemanticModel.GetDeclaredSymbol(field.Declaration.Variables[0], context.CancellationToken)
            is IFieldSymbol { IsStatic: false, ContainingType: { IsReadOnly: true, IsValueType: true } }) {
            return;
        }

        // ⚠ One finding per declaration, not per declarator. `readonly SpinLock a, b;` has one
        // `readonly` token, so two findings would carry the same deletion twice and `skala fix`
        // would apply it twice over a span that no longer exists.
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                field.Declaration.Variables[0].Identifier.GetLocation(),
                FixEdits.Pack((TextSpan.FromBounds(keyword.SpanStart, keyword.GetNextToken().SpanStart), string.Empty)),
                "`SpinLock` is a mutable struct, so a `readonly` field is locked on a copy and excludes nothing"
            )
        );
    }
}
