using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4021</c> — a <c>private</c> instance method whose body never reaches instance state.
/// </summary>
/// <remarks>
///     ⚠ <c>private</c> is not a conservative starting point that will be widened later; it is the
///     boundary of what the edit can prove. <c>static</c> on a visible member is an API change, and
///     <c>instance.Method()</c> at any call site stops compiling — a private member's call sites are
///     the ones this rule can see and check.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatelessPrivateMethodAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.StatelessPrivateMethod);

    static readonly SyntaxKind[] Excluded = [
        SyntaxKind.StaticKeyword, SyntaxKind.AbstractKeyword, SyntaxKind.VirtualKeyword,
        SyntaxKind.OverrideKeyword, SyntaxKind.PartialKeyword, SyntaxKind.ExternKeyword,
        SyntaxKind.NewKeyword, SyntaxKind.UnsafeKeyword
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (MethodDeclarationSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (declaration is { Body: null, ExpressionBody: null }
            || declaration.AttributeLists.Count != 0
            || Excluded.Any(kind => declaration.Modifiers.Any(kind))
            || declaration.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static type => type.Modifiers.Any(SyntaxKind.PartialKeyword))
            || model.GetDeclaredSymbol(declaration, cancellation) is not {
                MethodKind: MethodKind.Ordinary,
                IsStatic: false,
                DeclaredAccessibility: Accessibility.Private
            } method
            || method.ExplicitInterfaceImplementations.Length != 0
            || method.ContainingType is not {
                TypeKind: TypeKind.Class or TypeKind.Struct,
                DeclaringSyntaxReferences.Length: 1
            }) {
            return;
        }

        if (!CaptureProof.UsesNothingOutside(
                model,
                declaration,
                cancellation,
                symbol => SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, method)
            )
            || ReachedThroughAnInstance(model, declaration, method, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                FixEdits.Pack((new TextSpan(Insertion(declaration), 0), "static ")),
                "`" + method.Name + "` uses no instance state; mark it `static`"
            )
        );
    }

    /// <summary>
    ///     ⚠ The other half of the proof, and the half a body-only analysis misses. A body that never
    ///     touches <c>this</c> still cannot become <c>static</c> while one call site writes
    ///     <c>this.Method()</c> or <c>other.Method()</c>: that spelling is an error against a static
    ///     method, so the "safe" fix would break the build.
    /// </summary>
    static bool ReachedThroughAnInstance(
        SemanticModel model,
        MethodDeclarationSyntax declaration,
        IMethodSymbol method,
        CancellationToken cancellation
    ) {
        foreach (var name in declaration.SyntaxTree.GetRoot(cancellation)
                     .DescendantNodes()
                     .OfType<SimpleNameSyntax>()) {
            cancellation.ThrowIfCancellationRequested();
            if (name.Identifier.ValueText != method.Name) {
                continue;
            }

            var throughAReceiver = name.Parent switch {
                MemberAccessExpressionSyntax access => access.Name == name,
                MemberBindingExpressionSyntax binding => binding.Name == name,
                _ => false
            };
            if (!throughAReceiver) {
                continue;
            }

            if (model.GetSymbolInfo(name, cancellation).Symbol is IMethodSymbol reference
                && SymbolEqualityComparer.Default.Equals(reference.OriginalDefinition, method)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     After the accessibility and before everything else: <c>private static async Task</c>, which
    ///     is the order the arranger already writes.
    /// </summary>
    static int Insertion(MethodDeclarationSyntax declaration) {
        foreach (var modifier in declaration.Modifiers) {
            if (!modifier.IsKind(SyntaxKind.PrivateKeyword)
                && !modifier.IsKind(SyntaxKind.ProtectedKeyword)
                && !modifier.IsKind(SyntaxKind.InternalKeyword)
                && !modifier.IsKind(SyntaxKind.PublicKeyword)
                && !modifier.IsKind(SyntaxKind.FileKeyword)) {
                return modifier.SpanStart;
            }
        }

        return declaration.ReturnType.SpanStart;
    }
}
