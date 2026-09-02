using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2133</c> — a <c>partial void</c> with no implementation that something calls anyway.
/// </summary>
/// <remarks>
///     ⚠ <b>A <c>partial</c> method with no implementation is legal, and erasing it is the feature —
///     so the declaration on its own is never the finding.</b> The call is. When no implementing
///     declaration exists the compiler removes the defining declaration <em>and every call to it,
///     arguments included</em>, so a statement that reads as a call to a hook runs nothing, and any
///     work written into its arguments is deleted with it.
///     <para>
///         ⚠ <b>The other half of issue #186 is a compile error and was verified as one.</b> A C# 9
///         extended partial method — one with an accessibility modifier, a non-<c>void</c> return, or
///         an <c>out</c> parameter — <b>must</b> have an implementation, and a probe compiling one
///         without gives <c>CS8795</c>. That half needs no rule, cannot reach a compiling analysis, and
///         is excluded here by requiring <c>void</c> rather than by hoping. What is left is the classic
///         form, on which the same probe draws nothing at all: no <c>CS</c>, no <c>CA</c>, at
///         <c>AnalysisMode=All</c>.
///     </para>
///     <para>
///         ⚠ <b>Requiring a call is what makes the rule decidable rather than merely narrow.</b> A
///         classic <c>partial void</c> may carry no accessibility modifier and is therefore implicitly
///         <c>private</c>, so every caller it can ever have is inside the declaring type — which is in
///         this compilation, in full. "Nothing calls it" and "something calls it" are both facts here,
///         not guesses about the rest of the world, which is the difference between this rule and the
///         field half of #24.
///     </para>
///     <para>
///         An uncalled unimplemented <c>partial void</c> is left alone deliberately: it is an extension
///         point nobody has taken up, it costs nothing at runtime, and reporting it would put a finding
///         on the feature rather than on a mistake.
///     </para>
///     <para>
///         Report-only, and the two repairs are why. Writing the implementing part is the fix when the
///         hook was meant to do something; deleting the declaration and its calls is the fix when it
///         was not. Nothing in the source says which, and an edit that guessed would either invent a
///         method body or silently delete the arguments the finding exists to point at.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnimplementedPartialMethodAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnimplementedPartialMethod);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    /// <remarks>
    ///     ⚠ <c>GeneratedCodeAnalysisFlags.Analyze</c> rather than <c>None</c>, for the same reason
    ///     <c>SK2131</c> uses it: a call from a generated <c>partial</c> part is a call, and an
    ///     implementation in a generated part is an implementation. The finding is about a hook the
    ///     <em>compilation</em> leaves empty, not about one file. Roslyn still drops any diagnostic
    ///     whose location is generated, because <c>ReportDiagnostics</c> is not set.
    /// </remarks>
    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterSymbolStartAction(OnType, SymbolKind.NamedType);
    }

    /// <summary>
    ///     One type at a time, so every <c>partial</c> part is in scope without an analyzer asking the
    ///     compilation for a semantic model (RS1030).
    /// </summary>
    static void OnType(SymbolStartAnalysisContext context) {
        // ⚠ A symbol-level pre-filter that is exact rather than approximate: the question the rule
        // asks is answerable from the member list alone, so a type with no unimplemented `partial
        // void` registers no syntax action at all.
        var type = (INamedTypeSymbol)context.Symbol;
        if (!type.GetMembers().OfType<IMethodSymbol>().Any(Unimplemented)) {
            return;
        }

        var declarations = new ConcurrentDictionary<ISymbol, Location>(SymbolEqualityComparer.Default);
        var calls = new ConcurrentBag<(ISymbol Method, bool SideEffecting)>();

        context.RegisterSyntaxNodeAction(
            node => {
                var method = (MethodDeclarationSyntax)node.Node;
                if (!method.Modifiers.Any(SyntaxKind.PartialKeyword)
                    || method.Body is not null
                    || method.ExpressionBody is not null) {
                    return;
                }

                if (node.SemanticModel.GetDeclaredSymbol(method, node.CancellationToken) is IMethodSymbol symbol
                    && Unimplemented(symbol)) {
                    declarations.TryAdd(symbol.OriginalDefinition, method.Identifier.GetLocation());
                }
            },
            SyntaxKind.MethodDeclaration
        );

        context.RegisterSyntaxNodeAction(
            node => {
                var invocation = (InvocationExpressionSyntax)node.Node;
                if (node.SemanticModel.GetSymbolInfo(invocation, node.CancellationToken).Symbol
                    is not IMethodSymbol called) {
                    return;
                }

                // An invocation of a partial method with no implementation binds to the defining
                // declaration; `PartialDefinitionPart` covers the other direction defensively.
                var definition = (called.PartialDefinitionPart ?? called).OriginalDefinition;
                if (Unimplemented(definition)) {
                    calls.Add(
                        (definition, invocation.ArgumentList.Arguments.Any(static a => DoesWork(a.Expression)))
                    );
                }
            },
            SyntaxKind.InvocationExpression
        );

        context.RegisterSymbolEndAction(
            end => {
                foreach (var pair in declarations) {
                    var count = 0;
                    var sideEffecting = false;
                    foreach (var call in calls) {
                        if (!SymbolEqualityComparer.Default.Equals(call.Method, pair.Key)) {
                            continue;
                        }

                        count++;
                        sideEffecting |= call.SideEffecting;
                    }

                    if (count == 0) {
                        continue;
                    }

                    end.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptor,
                            pair.Value,
                            "`"
                            + pair.Key.Name
                            + "` has no implementing declaration, so it and its "
                            + (count == 1 ? "one call site are" : count + " call sites are")
                            + " erased"
                            + (sideEffecting
                                ? " — including the arguments, one of which does work that will therefore not happen"
                                : "")
                        )
                    );
                }
            }
        );
    }

    /// <summary>
    ///     A classic <c>partial void</c> definition that nothing implements.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ReturnsVoid</c> is what keeps the C# 9 extended form out, and it is a statement about
    ///     the compiler rather than a preference: an extended partial method with no implementation is
    ///     <c>CS8795</c>, so it can never reach a compiling analysis in the first place.
    /// </remarks>
    static bool Unimplemented(IMethodSymbol method) =>
        method is { IsPartialDefinition: true, PartialImplementationPart: null, ReturnsVoid: true };

    /// <summary>
    ///     Whether evaluating this argument would have done something the erasure now skips.
    /// </summary>
    /// <remarks>
    ///     Deliberately syntactic and deliberately generous about what counts: a call, a construction,
    ///     an <c>await</c>, an assignment or an increment anywhere inside the argument is enough. It
    ///     changes only the message's last clause, never whether the finding exists, so being wrong in
    ///     the loud direction costs a sentence rather than a false positive.
    /// </remarks>
    static bool DoesWork(SyntaxNode argument) {
        foreach (var node in argument.DescendantNodesAndSelf()) {
            switch (node.Kind()) {
                case SyntaxKind.InvocationExpression:
                case SyntaxKind.ObjectCreationExpression:
                case SyntaxKind.ImplicitObjectCreationExpression:
                case SyntaxKind.AwaitExpression:
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PreDecrementExpression:
                case SyntaxKind.PostIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    return true;

                default:
                    continue;
            }
        }

        return false;
    }
}
