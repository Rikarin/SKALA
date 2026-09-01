using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Design;

/// <summary>
///     <c>SK6050</c> — a <c>private</c> method that takes arguments, reads none of them, and returns a
///     compile-time constant.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         The legitimate forms of "returns a constant" outnumber the defective ones, and
///         <c>private</c> is what separates them — not a conservative default that will be widened later.
///     </b>
///     An interface implementation, a <c>virtual</c> hook meant to be overridden, a test double and a
///     strategy that genuinely is constant are all methods whose body is a constant on purpose, and every
///     one of them is reached from outside the type. A <c>private</c> method is the only case where the
///     rule can see every caller, and the only case where "the parameters are computed and thrown away"
///     is a statement about the program rather than about one declaration.
///     <para>
///         ⚠ The recall cost is the whole public and internal surface, which is where an AI-written stub
///         is most likely to be. That is a deliberate trade: doc 08's bar is zero false positives, and
///         there is no predicate over a visible method that separates a placeholder from a deliberate
///         constant.
///     </para>
///     <para>
///         ⚠ A method group withdraws the finding. <c>private static bool Always(Item i) =&gt; true;</c>
///         passed to <c>Where</c> is a predicate whose whole point is that it ignores its input, and it is
///         indistinguishable from a stub by anything except how it is used. The scan is the one
///         <c>SK4021</c> uses and is sound for the same reason: the containing type is required to have a
///         single declaration, so every reference to a private member is in that one file.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantReturningMethodAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MethodReturnsAConstant);

    static readonly SyntaxKind[] Excluded = [
        SyntaxKind.AbstractKeyword, SyntaxKind.VirtualKeyword, SyntaxKind.OverrideKeyword,
        SyntaxKind.PartialKeyword, SyntaxKind.ExternKeyword, SyntaxKind.NewKeyword,
        SyntaxKind.AsyncKeyword
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

        // ⚠ An attribute is a reason the body is what it is that the rule cannot read. `[Conditional]`,
        // a source generator's marker and a serializer's hook all make a constant body deliberate.
        if (declaration.AttributeLists.Count != 0
            || Excluded.Any(kind => declaration.Modifiers.Any(kind))
            || model.GetDeclaredSymbol(declaration, cancellation) is not {
                MethodKind: MethodKind.Ordinary,
                DeclaredAccessibility: Accessibility.Private,
                ReturnsVoid: false,
                ReturnsByRef: false,
                ReturnsByRefReadonly: false
            } method
            // An explicit interface implementation is `private` to the symbol layer and is the exact
            // shape this rule must never report: the signature is somebody else's.
            || method.ExplicitInterfaceImplementations.Length != 0
            || method.Parameters.Length == 0
            || method.ContainingType is not {
                TypeKind: TypeKind.Class or TypeKind.Struct,
                DeclaringSyntaxReferences.Length: 1
            }) {
            return;
        }

        if (Returned(declaration) is not { } value || value.DescendantNodesAndSelf().Any(IsNameOf)) {
            return;
        }

        if (!model.GetConstantValue(value, cancellation).HasValue) {
            return;
        }

        if (ReferencedAsAMethodGroup(model, declaration, method, cancellation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                declaration.Identifier.GetLocation(),
                "`"
                + method.Name
                + "` reads none of its "
                + method.Parameters.Length
                + " parameter(s) and always returns `"
                + value
                + "`"
            )
        );
    }

    /// <summary>
    ///     The single expression the method returns, or null where there is more than one path.
    /// </summary>
    /// <remarks>
    ///     ⚠ One statement, not "every return is the same constant". A method with two returns has a
    ///     branch, and a branch is a method that reads something — if not a parameter then state, and
    ///     either way not the placeholder this rule is about.
    /// </remarks>
    static ExpressionSyntax? Returned(MethodDeclarationSyntax declaration) {
        if (declaration.ExpressionBody is { Expression: { } arrow }) {
            return arrow;
        }

        // ⚠ Written out rather than as a list pattern: the analyzer targets netstandard2.0 (ADR-006),
        // which has no `System.Index`, and a list pattern needs one.
        return declaration.Body is { Statements.Count: 1 }
            && declaration.Body.Statements[0] is ReturnStatementSyntax { Expression: { } single }
                ? single
                : null;
    }

    /// <summary>
    ///     ⚠ <c>nameof(parameter)</c> is a constant that reads its input, which is the one way the two
    ///     halves of this rule's predicate can both hold and the finding still be wrong.
    /// </summary>
    static bool IsNameOf(SyntaxNode node) =>
        node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } };

    /// <summary>
    ///     Whether the method is ever named without being called, anywhere in the file that declares it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The soundness rests on <c>DeclaringSyntaxReferences.Length == 1</c>, checked by the caller. A
    ///     private member of a type declared in one place has all of its references in that one file, so
    ///     one tree is the whole search; the same guard is what makes <c>SK4021</c>'s call-site scan a
    ///     proof rather than a sample. <c>nameof(Method)</c> reaches here as a non-invocation reference and
    ///     withdraws the finding too, which is conservative and correct: a name written down somewhere is a
    ///     use the rule cannot follow.
    /// </remarks>
    static bool ReferencedAsAMethodGroup(
        SemanticModel model,
        MethodDeclarationSyntax declaration,
        IMethodSymbol method,
        CancellationToken cancellation
    ) {
        foreach (var name in declaration.SyntaxTree.GetRoot(cancellation)
                     .DescendantNodes()
                     .OfType<SimpleNameSyntax>()) {
            cancellation.ThrowIfCancellationRequested();
            if (name.Identifier.ValueText != method.Name || IsInvoked(name)) {
                continue;
            }

            if (model.GetSymbolInfo(name, cancellation).Symbol is IMethodSymbol reference
                && SymbolEqualityComparer.Default.Equals(reference.OriginalDefinition, method)) {
                return true;
            }
        }

        return false;
    }

    static bool IsInvoked(SimpleNameSyntax name) {
        var expression = name.Parent switch {
            MemberAccessExpressionSyntax access when access.Name == name => (ExpressionSyntax)access,
            MemberBindingExpressionSyntax binding when binding.Name == name => binding,
            _ => name
        };

        return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
    }
}
