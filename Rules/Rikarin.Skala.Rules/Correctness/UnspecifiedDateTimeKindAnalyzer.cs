using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2161</c> — a <c>DateTime</c> whose <c>Kind</c> is <c>Unspecified</c> is converted into an
///     absolute instant, so the machine's own time zone decides what moment it means.
/// </summary>
/// <remarks>
///     ⚠ <b>Reporting every <c>DateTime</c> would be absurd, so the rule reports the escape rather than
///     the value.</b> A <c>DateTime</c> that is only ever compared with, formatted from or stored beside
///     other values of the same unstated zone is internally consistent and no worse than the domain it
///     models. The defect appears at the one point where the value is turned into a fixed moment on the
///     world's timeline, because that conversion has to supply an offset and it takes the offset from
///     whichever machine happens to be running.
///     <para>
///         ⚠ <b>The sharpest illustration is that the two conversions disagree with each other.</b>
///         <c>ToUniversalTime()</c> on an <c>Unspecified</c> value assumes it was <em>local</em> and
///         subtracts the machine's offset; <c>ToLocalTime()</c> on the same value assumes it was
///         <em>UTC</em> and adds it. One value, one <c>Kind</c>, two opposite readings — which is why
///         "it round-trips fine on my machine" is not evidence of anything.
///     </para>
///     <para>
///         ⚠ <b><c>Kind</c> is proved from the constructor's parameters, never guessed.</b> The only
///         source this rule treats as <c>Unspecified</c> is a <c>new DateTime(…)</c> whose overload takes
///         no <c>DateTimeKind</c>, read either directly or through a local that is assigned exactly once.
///         A value that arrived from a parameter, a field, a parse or another assembly has a
///         <c>Kind</c> nobody here can prove, and an unproved <c>Kind</c> is silence.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnspecifiedDateTimeKindAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnspecifiedDateTimeKind);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeCreation, SyntaxKind.ObjectCreationExpression);

        // ⚠ The implicit `DateTime`-to-`DateTimeOffset` conversion has no syntax of its own —
        // `DateTimeOffset when = built;` contains no `new` and no cast — so it is unreachable from a
        // syntax registration and needs the operation tree. It is the *commonest* spelling of this
        // defect, which is why it is worth a second registration rather than a stated gap.
        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
    }

    /// <summary>
    ///     <c>ToUniversalTime()</c> and <c>ToLocalTime()</c>, whose whole behaviour is a function of the
    ///     <c>Kind</c> they are given.
    /// </summary>
    static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || (access.Name.Identifier.ValueText != "ToUniversalTime"
                && access.Name.Identifier.ValueText != "ToLocalTime")) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetOperation(invocation, cancellation) is not IInvocationOperation {
                Instance: { } instance
            } call
            || !Clock.IsFrameworkType(call.TargetMethod.ContainingType, context.Compilation, "System.DateTime")
            || !IsUnspecified(instance, model, cancellation, context.Compilation)) {
            return;
        }

        // ⚠ The two assume opposite things about the same `Kind`, so the message names which one this
        // call picked rather than saying "a time zone is missing".
        var assumed = call.TargetMethod.Name == "ToUniversalTime" ? "local" : "UTC";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "`"
                + call.TargetMethod.Name
                + "` is called on a `DateTime` built with no `DateTimeKind`, so it is treated as "
                + assumed
                + " time on whichever machine runs it; state the kind, or use `DateTimeOffset`"
            )
        );
    }

    /// <summary>
    ///     <c>new DateTimeOffset(dateTime)</c>, which supplies the local offset for a value that never
    ///     claimed to be local.
    /// </summary>
    /// <remarks>
    ///     The single-argument overload is the only one matched: every other one is handed an offset, and
    ///     an offset that was written down is the repair rather than the defect. The implicit conversion
    ///     spelling is a separate path — see <see cref="AnalyzeConversion" />.
    /// </remarks>
    static void AnalyzeCreation(SyntaxNodeAnalysisContext context) {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (model.GetOperation(creation, cancellation) is not IObjectCreationOperation {
                Arguments.Length: 1
            } operation
            || !Clock.IsFrameworkType(operation.Type, context.Compilation, "System.DateTimeOffset")
            || !IsUnspecified(operation.Arguments[0].Value, model, cancellation, context.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                creation.GetLocation(),
                "a `DateTime` built with no `DateTimeKind` is converted to a `DateTimeOffset`, which "
                + "takes the running machine's offset for a value that never said it was local; pass "
                + "the offset the value actually has"
            )
        );
    }

    /// <summary>
    ///     The <c>DateTime</c>-to-<c>DateTimeOffset</c> conversion, implicit or written as a cast.
    /// </summary>
    /// <remarks>
    ///     ⚠ <b>Only that one conversion.</b> The operation tree is full of conversions — every boxing,
    ///     every numeric widening, every reference upcast — so the guard is the pair of types rather than
    ///     the operation kind, and it is checked before anything else is asked. An implicit conversion
    ///     that the compiler inserted around a value the author never converted is exactly what this rule
    ///     is about: it is where the offset is silently supplied.
    /// </remarks>
    static void AnalyzeConversion(OperationAnalysisContext context) {
        var conversion = (IConversionOperation)context.Operation;
        if (!Clock.IsFrameworkType(conversion.Type, context.Compilation, "System.DateTimeOffset")
            || !Clock.IsFrameworkType(conversion.Operand.Type, context.Compilation, "System.DateTime")
            || conversion.SemanticModel is not { } model
            || !IsUnspecified(conversion.Operand, model, context.CancellationToken, context.Compilation)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                conversion.Syntax.GetLocation(),
                "a `DateTime` built with no `DateTimeKind` becomes a `DateTimeOffset` here, which takes "
                + "the running machine's offset for a value that never said it was local; state the "
                + "kind, or carry the offset"
            )
        );
    }

    /// <summary>
    ///     Whether a value is provably a <c>DateTime</c> constructed without a <c>DateTimeKind</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two shapes only: the creation written in place, and a local assigned exactly once from one.
    ///     A conversion wrapper is unwrapped first, because the implicit <c>DateTime</c>-to-
    ///     <c>DateTimeOffset</c> conversion arrives here as an <c>IConversionOperation</c> around the
    ///     creation and matching on the conversion's own type would find nothing.
    /// </remarks>
    static bool IsUnspecified(
        IOperation value,
        SemanticModel model,
        CancellationToken cancellation,
        Compilation compilation
    ) {
        var unwrapped = value;
        while (unwrapped is IConversionOperation conversion) {
            unwrapped = conversion.Operand;
        }

        if (Clock.IsUnspecifiedCreation(unwrapped, compilation)) {
            return true;
        }

        if (unwrapped is not ILocalReferenceOperation { Local: { } local }
            || Clock.SingleAssignedInitializer(local, model, cancellation) is not { } initializer) {
            return false;
        }

        return Clock.IsUnspecifiedCreation(model.GetOperation(initializer, cancellation), compilation);
    }
}
