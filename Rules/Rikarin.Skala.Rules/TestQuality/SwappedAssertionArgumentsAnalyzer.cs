using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.TestQuality;

/// <summary>
///     <c>SK8022</c> — the constant is in the <c>actual</c> position, so the failure message reads
///     backwards.
/// </summary>
/// <remarks>
///     ⚠ The test still passes and fails on exactly the same inputs; only the diagnosis is inverted.
///     "Expected: 3, Actual: 0" against a swapped call means the code produced 3 and the author wanted 0,
///     and every reading of the failure starts from a reversed diff. That is why it is a `warning` about a
///     test that is behaving correctly.
///     <para>
///         ⚠ <b>The whole rule is one asymmetry: a constant cannot be produced by the code under test.</b>
///         When the <c>expected</c> argument is not a constant and the <c>actual</c> argument is, the two
///         are the wrong way round and nothing has to be inferred about intent. When both are constants or
///         neither is, the rule says nothing — there is no evidence either way and guessing would be a
///         rewrite of somebody's test.
///     </para>
///     <para>
///         ⚠ All three frameworks, and the parameter <em>names</em> are the guard rather than a list of
///         signatures: the method is only matched when its two parameters are called <c>expected</c> and
///         <c>actual</c>, in that order. An overload that means something else by its arguments cannot
///         match by accident, and NUnit's constraint form — <c>Assert.That(actual, Is.EqualTo(expected))</c>,
///         which is the opposite order and correct — is excluded by the same guard rather than by a name.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwappedAssertionArgumentsAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SwappedAssertionArguments);

    /// <summary>
    ///     ⚠ The assertion classes, by full name. A repository's own <c>Assert</c> is not one of them.
    /// </summary>
    static readonly ImmutableHashSet<string> AssertTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Xunit.Assert",
        "Microsoft.VisualStudio.TestTools.UnitTesting.Assert",
        "NUnit.Framework.Assert",
        "NUnit.Framework.Legacy.ClassicAssert"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 2) {
            return;
        }

        var expected = invocation.ArgumentList.Arguments[0];
        var actual = invocation.ArgumentList.Arguments[1];

        // ⚠ A named argument states the order explicitly, so there is nothing to be wrong about and
        // the positional swap the fix performs would be a no-op that changed the meaning.
        if (expected.NameColon is not null || actual.NameColon is not null) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation call
            || !AssertTypes.Contains(call.TargetMethod.ContainingType.ToDisplayString())
            || call.TargetMethod.Parameters.Length != 2
            || call.TargetMethod.Parameters[0].Name != "expected"
            || call.TargetMethod.Parameters[1].Name != "actual") {
            return;
        }

        // ⚠ Both parameters must have the same type, and that is what makes the fix safe rather than
        // plausible. Overload resolution and generic inference over two arguments of one parameter
        // type are symmetric, so the swapped call binds to the same method with the same type
        // arguments; if the two parameter types differed, the reversed call could bind elsewhere or
        // fail to infer at all — the shape docs/plan/08 records `SK8002` breaking on.
        if (!SymbolEqualityComparer.Default.Equals(
                call.TargetMethod.Parameters[0].Type,
                call.TargetMethod.Parameters[1].Type
            )) {
            return;
        }

        // ⚠ The asymmetry, and the whole rule. A constant is also side-effect free, which is what
        // makes reordering the two arguments unobservable as well as type-safe.
        if (model.GetConstantValue(expected.Expression, cancellation).HasValue
            || !model.GetConstantValue(actual.Expression, cancellation).HasValue) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.ArgumentList.GetLocation(),
                FixEdits.Pack(
                    (expected.Expression.Span, actual.Expression.ToString()),
                    (actual.Expression.Span, expected.Expression.ToString())
                ),
                "`"
                + call.TargetMethod.Name
                + "` is called as (actual, expected), so its failure message will name the two the wrong "
                + "way round"
            )
        );
    }
}
