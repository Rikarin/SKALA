using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2164</c> — the argument of a call the compiler may delete carries a side effect, so the
///     program does one thing in a debug build and another in the build that ships.
/// </summary>
/// <remarks>
///     ⚠ <b><c>Debug.Assert</c> is <c>[Conditional("DEBUG")]</c>, which does not mean the condition
///     evaluates to <c>true</c> in release — it means the <em>call site is deleted</em>, arguments and
///     all.</b> <c>Debug.Assert(items.Remove(key))</c> removes the item in every debug run and in no
///     release run. The resulting bug exists only in production, disappears the moment anybody attaches a
///     debugger or runs the test suite, and every reproduction attempt confirms the code is fine.
///     <para>
///         ⚠ <b>The rule is on <c>[Conditional]</c>, not on a list of assertion methods, and that is a
///         generalisation that costs nothing.</b> <c>Debug.Assert</c> and <c>Trace.Assert</c> are the
///         motivating case and the commonest one, but the defect is a property of the attribute: a
///         repository's own <c>[Conditional("TRACE")]</c> logging helper deletes its arguments exactly the
///         same way, and a rule naming only the framework's two would be silent on the version somebody
///         wrote themselves.
///     </para>
///     <para>
///         ⚠ <b>An xUnit, NUnit or MSTest assertion is out of scope by construction, which is the answer
///         to the shape that would otherwise be this rule's worst false positive.</b>
///         <c>Assert.True(map.TryGetValue(key, out var found))</c> is idiomatic and correct: none of the
///         three frameworks marks its assertions <c>[Conditional]</c>, so the call is never deleted, the
///         effect always happens, and there is no defect to report. This rule cannot reach it, rather
///         than reaching it and filtering it out.
///     </para>
///     <para>
///         ⚠ <b>An <c>out var</c> the code below reads was built as a fifth kind of evidence and
///         then removed, because the compiler already reports it.</b> With the call deleted the
///         variable is never assigned, so the reader below it is <c>CS0165</c> — <i>use of unassigned
///         local variable</i> — in any build without the symbol defined. The positive fixture written
///         for it could not be made to compile, which is how this was found rather than argued.
///         docs/plan/08 § "the compiler already says it": a rule that restates a compiler error adds
///         a second voice and no information.
///     </para>
///     <para>
///         ⚠ <b>What counts as a side effect is enumerated, never inferred.</b> "Does this method mutate"
///         is undecidable, and a rule that guessed would report <c>Debug.Assert(list.Any())</c> and be
///         switched off the same afternoon. Four kinds of evidence are accepted, each of which is a fact
///         about the syntax or about a framework symbol — see the members below.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SideEffectInAssertionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SideEffectInAssertion);

    /// <summary>
    ///     ⚠ The mutating collection members, matched by name <em>and</em> by a namespace on the list
    ///     below.
    /// </summary>
    /// <remarks>
    ///     A user type's <c>Remove</c> is not matched, because the rule cannot know what it does.
    /// </remarks>
    static readonly ImmutableHashSet<string> Mutators = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Add",
        "AddOrUpdate",
        "Clear",
        "Dequeue",
        "Enqueue",
        "GetOrAdd",
        "Insert",
        "MoveNext",
        "Pop",
        "Push",
        "Remove",
        "RemoveAll",
        "RemoveAt",
        "TryAdd",
        "TryDequeue",
        "TryPop",
        "TryRemove",
        "TryTake"
    );

    /// <summary>
    ///     ⚠ <b>The mutable collection namespaces, listed exactly — and the reason is
    ///     <c>System.Collections.Immutable</c>.</b>
    /// </summary>
    /// <remarks>
    ///     A prefix test on <c>"System.Collections"</c> reads naturally and is wrong:
    ///     <c>ImmutableList&lt;T&gt;.Add</c> and <c>ImmutableDictionary&lt;K, V&gt;.Remove</c> return a new
    ///     collection and mutate nothing, so every one of them would be a false positive under a prefix
    ///     match. <c>System.Collections.Frozen</c> is excluded for the same reason.
    /// </remarks>
    static readonly ImmutableHashSet<string> Namespaces = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Collections",
        "System.Collections.Generic",
        "System.Collections.Concurrent",
        "System.Collections.ObjectModel",
        "System.Collections.Specialized"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ Without `ConditionalAttribute` nothing in the compilation can be compiled out, so
                // the whole rule withdraws rather than resolving a symbol per invocation.
                var conditional = start.Compilation.GetTypeByMetadataName("System.Diagnostics.ConditionalAttribute");
                if (conditional is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, conditional),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol conditional) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || !IsConditional(call.TargetMethod, conditional)) {
            return;
        }

        // ⚠ Every argument, not only the condition. The call site is deleted whole, so an effect
        // written in the message argument disappears with it.
        foreach (var argument in invocation.ArgumentList.Arguments) {
            if (Effect(argument.Expression, model, cancellation, context.Compilation) is not { } what) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    argument.Expression.GetLocation(),
                    "`"
                    + call.TargetMethod.ContainingType.Name
                    + "."
                    + call.TargetMethod.Name
                    + "` is conditionally compiled, so this "
                    + what
                    + " happens in some builds and not others"
                )
            );
            return;
        }
    }

    /// <summary>Whether a method's call sites can be deleted by the compiler.</summary>
    /// <remarks>
    ///     ⚠ The attribute is looked for on the method itself and on the definition it overrides, because
    ///     <c>[Conditional]</c> is inherited by an override and reading only the immediate symbol would
    ///     miss it.
    /// </remarks>
    static bool IsConditional(IMethodSymbol method, INamedTypeSymbol conditional) {
        for (var current = method; current is not null; current = current.OverriddenMethod) {
            foreach (var attribute in current.GetAttributes()) {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, conditional)) {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     What the argument does that it should not, or <c>null</c> when nothing here can prove it does
    ///     anything.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk descends into lambdas on purpose. The whole argument is deleted, so an effect
    ///     written inside a lambda the argument passes to <c>All</c> or <c>Any</c> is deleted with it —
    ///     stopping at the lambda boundary would be silent on the shape most likely to hide one.
    /// </remarks>
    static string? Effect(
        ExpressionSyntax argument,
        SemanticModel model,
        CancellationToken cancellation,
        Compilation compilation
    ) {
        foreach (var node in argument.DescendantNodesAndSelf()) {
            switch (node) {
                // (1) An assignment, in any of its spellings.
                case AssignmentExpressionSyntax:
                    return "assignment";

                // (2) An increment or a decrement.
                case PrefixUnaryExpressionSyntax prefix when IsStep(prefix.Kind()):
                case PostfixUnaryExpressionSyntax postfix when IsStep(postfix.Kind()):
                    return "increment";

                // (3) An `await`: the awaited work is what disappears, not merely its result.
                case AwaitExpressionSyntax:
                    return "`await`";

                // (4) A call to a framework collection member whose contract is to mutate.
                case InvocationExpressionSyntax inner when IsMutator(inner, model, cancellation, compilation):
                    return "collection mutation";
            }
        }

        return null;
    }

    static bool IsStep(SyntaxKind kind) =>
        kind == SyntaxKind.PreIncrementExpression
        || kind == SyntaxKind.PreDecrementExpression
        || kind == SyntaxKind.PostIncrementExpression
        || kind == SyntaxKind.PostDecrementExpression;

    /// <summary>Whether an invocation is a framework collection member whose contract is to mutate.</summary>
    static bool IsMutator(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        CancellationToken cancellation,
        Compilation compilation
    ) {
        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || !Mutators.Contains(call.TargetMethod.Name)) {
            return false;
        }

        var owner = call.TargetMethod.ContainingType;

        // ⚠ Framework types only, matched through the containing namespace's full name. A source type
        // that happens to sit in a namespace of the same name is excluded by the metadata test, the
        // same guard every other rule in this band uses.
        return !owner.Locations.Any(static location => location.IsInSource)
            && Namespaces.Contains(owner.ContainingNamespace?.ToDisplayString() ?? string.Empty)
            && !SymbolEqualityComparer.Default.Equals(
                owner.ContainingAssembly,
                compilation.Assembly
            );
    }
}
