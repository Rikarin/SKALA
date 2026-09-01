using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2073</c> — an error-level log inside a <c>catch</c> that never gives the logger the
///     exception it caught.
/// </summary>
/// <remarks>
///     Every one of these APIs takes the exception in its own parameter, and that parameter is what a
///     sink treats as an exception: it is where the stack trace, the inner chain and the type end up as
///     structure rather than as a sentence. Left empty, the entry says something went wrong and carries
///     nothing anybody can act on — which is the whole reason the line was written.
///     <para>
///         ⚠ <b>Error and critical levels only.</b> An information- or debug-level line inside a
///         <c>catch</c> is very often deliberate — an expected exception, handled, noted in passing —
///         and reporting those is how a rule about logging becomes noise about control flow.
///     </para>
///     <para>
///         ⚠ <b>The fix is only offered where prepending an argument certainly binds.</b> It is emitted
///         when the template is the first argument the call actually writes, which is the
///         <c>Log*(template, values…)</c> shape. It is <em>not</em> emitted for
///         <c>LogError(eventId, template, …)</c>, and that is a correctness constraint rather than
///         caution: <c>Microsoft.Extensions.Logging</c> orders that overload
///         <c>
/// (EventId, Exception,
///         string)
///         </c>, so an exception prepended in front of the event id does not bind and
///         <c>skala fix</c> would have broken the build on the tool's own advice. The rule declines
///         those calls outright rather than reporting a finding it cannot repair.
///     </para>
///     <para>
///         ⚠ <b>The exception overload is looked up before anything is reported.</b> A logging type
///         that has no overload taking an <c>exception</c> parameter has no defect to describe, and
///         checking is what keeps the fix from depending on an overload that may not exist.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaughtExceptionNotLoggedAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CaughtExceptionNotLogged);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var loggers = MessageTemplate.ResolveLoggers(start.Compilation);
                if (loggers.IsEmpty) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, loggers),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, ImmutableArray<INamedTypeSymbol> loggers) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation operation
            || !MessageTemplate.DeclaredBy(operation, loggers)
            || !IsErrorLevel(operation.TargetMethod.Name)
            || MessageTemplate.FindTemplate(operation) is not { } template) {
            return;
        }

        if (HasExceptionArgument(operation)) {
            return;
        }

        // ⚠ Nothing is reported unless the repair exists. A logging type with no `exception`
        // overload has no defect this rule can name, and looking it up is also what keeps the fix
        // from assuming an overload into being.
        if (!HasExceptionOverload(operation)) {
            return;
        }

        // ⚠ Only the `Log*(template, values…)` shape. See the type remarks: prepending an exception
        // in front of an EventId does not bind, and a fix that does not compile is worse than none.
        if (invocation.ArgumentList.Arguments.Count == 0
            || invocation.ArgumentList.Arguments[0] != template.Syntax) {
            return;
        }

        if (EnclosingCatch(invocation) is not { Declaration.Identifier: var identifier }
            || identifier.IsKind(SyntaxKind.None)
            || identifier.ValueText.Length == 0) {
            return;
        }

        var insertion = new TextSpan(template.Syntax.SpanStart, 0);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((insertion, identifier.ValueText + ", ")),
                "the caught exception `"
                + identifier.ValueText
                + "` is not passed to the logger's `exception` parameter, so the stack trace and the "
                + "inner chain are not in the event"
            )
        );
    }

    static bool IsErrorLevel(string name) => name is "LogError" or "LogCritical" or "Error" or "Fatal";

    static bool HasExceptionArgument(IInvocationOperation operation) {
        foreach (var argument in operation.Arguments) {
            if (argument.Parameter?.Name == "exception") {
                return true;
            }
        }

        return false;
    }

    static bool HasExceptionOverload(IInvocationOperation operation) {
        var method = operation.TargetMethod.ReducedFrom ?? operation.TargetMethod;
        foreach (var candidate in method.ContainingType.GetMembers(method.Name)) {
            if (candidate is not IMethodSymbol other) {
                continue;
            }

            foreach (var parameter in other.Parameters) {
                if (parameter.Name == "exception") {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     The nearest <c>catch</c> this call sits inside, stopping at the member that declares it.
    /// </summary>
    /// <remarks>
    ///     ⚠ The walk stops at a member declaration and not at the first <c>try</c>: a local function
    ///     or a lambda written inside a <c>catch</c> block still closes over the exception variable, so
    ///     the call really can pass it, and stopping early would silently drop the case. It does stop
    ///     at a member, because a method called from a <c>catch</c> is not inside one.
    /// </remarks>
    static CatchClauseSyntax? EnclosingCatch(SyntaxNode node) {
        for (var current = node.Parent; current is not null; current = current.Parent) {
            switch (current) {
                case CatchClauseSyntax clause:
                    return clause;
                case MemberDeclarationSyntax:
                    return null;
                default:
                    continue;
            }
        }

        return null;
    }
}
