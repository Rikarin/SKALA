using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2101</c> — a <c>[Pure]</c> annotation on a method that returns nothing.
/// </summary>
/// <remarks>
///     ⚠ <b>There are at least three different <c>PureAttribute</c>s and they do not mean the same
///     thing</b>, so the rule resolves by namespace-qualified name and accepts exactly two.
///     <list type="bullet">
///         <item>
///             <c>System.Diagnostics.Contracts.PureAttribute</c> — the BCL one: "makes no visible state
///             change". A <c>void</c> method that makes no visible state change and returns nothing has no
///             observable effect at all, so either the annotation is wrong or the method is dead.
///         </item>
///         <item>
///             <c>JetBrains.Annotations.PureAttribute</c> — "the return value must be used". A <c>void</c>
///             method has no return value, so the annotation is inapplicable by its own definition.
///         </item>
///     </list>
///     <para>
///         ⚠ Anything else spelled <c>PureAttribute</c> is declined. A third-party attribute that happens
///         to share the simple name is under no obligation to mean either of those, and matching on the
///         short name is how an annotation rule acquires false positives on code it has never seen.
///     </para>
///     <para>
///         ⚠ A method with an <c>out</c> or <c>ref</c> parameter is declined even when it returns
///         <c>void</c>. Its results leave through the parameters, Code Contracts explicitly allowed that
///         shape, and "returns nothing" is not true of it in the sense the rule means.
///     </para>
///     <para>
///         This is the annotation <c>SK2002</c> reads to decide whether a discarded result matters, so
///         an annotation that cannot be true is load-bearing in Skala specifically.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PureAttributeOnVoidAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.PureAttributeOnVoid);

    static readonly string[] Accepted = [
        "System.Diagnostics.Contracts.PureAttribute",
        "JetBrains.Annotations.PureAttribute",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol { ReturnsVoid: true } method) {
            return;
        }

        foreach (var parameter in method.Parameters) {
            if (parameter.RefKind is RefKind.Out or RefKind.Ref) {
                return;
            }
        }

        // ⚠ One finding for the declaration, not one per annotation. Two `[Pure]` attributes from two
        // vendors on one `void` method is one mistake, and the crossing fixture that says so is
        // `boundary_two_vendors_of_pure.cs`: it looks like a repeated attribute and is not one, because
        // the two are different classes — which is exactly where SK2103's boundary is drawn.
        var annotations = new List<AttributeSyntax>();
        string? first = null;
        foreach (var list in AttributeContract.ListsOn(context.Node)) {
            foreach (var attribute in list.Attributes) {
                var type = AttributeContract.Resolve(context.SemanticModel, attribute, context.CancellationToken);
                if (type is null) {
                    continue;
                }

                var name = AttributeContract.NameOf(type);
                if (Array.IndexOf(Accepted, name) < 0) {
                    continue;
                }

                annotations.Add(attribute);
                first ??= name;
            }
        }

        if (annotations.Count == 0 || first is null) {
            return;
        }

        // ⚠ The fix removes *every* accepted annotation, not only the one the finding points at. A fix
        // that left the second one behind fired the rule again on its own output, which is a `skala
        // fix` loop — and the harness caught exactly that rather than the reasoning doing so.
        var edits = new List<(Microsoft.CodeAnalysis.Text.TextSpan Span, string Text)>();
        foreach (var span in AttributeContract.Removals(annotations)) {
            edits.Add((span, string.Empty));
        }

        if (edits.Count == 0) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                annotations[0].GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "`" + first + "` on `" + method.Name + "`, which returns nothing"
            )
        );
    }
}
