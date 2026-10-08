using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Linq;

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
///         </c>
///         , so an exception prepended in front of the event id does not bind and
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

        if (EnclosingCatch(invocation) is not { Declaration: { Identifier: var identifier } declaration }
            || identifier.IsKind(SyntaxKind.None)
            || identifier.ValueText.Length == 0) {
            return;
        }

        var written = FixRebind.Identifier(identifier.ValueText);
        if (!PassesTheCaughtException(context, invocation, declaration, written)) {
            return;
        }

        var insertion = new TextSpan(template.Syntax.SpanStart, 0);
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                FixEdits.Pack((insertion, written + ", ")),
                "the caught exception `"
                + identifier.ValueText
                + "` is not passed to the logger's `exception` parameter, so the stack trace and the "
                + "inner chain are not in the event"
            )
        );
    }

    /// <summary>
    ///     ⚠ Whether the call with the caught exception's name prepended binds that name to the
    ///     <c>catch</c> variable and passes it as the <c>exception</c> argument (#424).
    /// </summary>
    /// <remarks>
    ///     ⚠ The name is a lookup at the call, not a reference to the <c>catch</c>, and a lambda between
    ///     the two can declare its own: in
    ///     <c>
    /// catch (Exception ex) { items.ForEach(ex =&gt;
    ///     log.LogError("item {I} failed", ex)); }
    ///     </c>
    ///     the inserted <c>ex</c> is the item, and with an
    ///     <c>int</c> item the call binds the <c>EventId</c> overload instead (#412's audit). The call is
    ///     therefore bound as written, in place, and both the name and the overload are checked.
    /// </remarks>
    static bool PassesTheCaughtException(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        CatchDeclarationSyntax declaration,
        string written
    ) {
        var caught = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(SyntaxFactory.ParseToken(written)));
        var rewritten = invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(invocation.ArgumentList.Arguments.Insert(0, argument))
        );
        if (caught is null
            || !FixRebind.TrySpeculate(context.SemanticModel, invocation, rewritten, out var model, out var placed)
            || placed is not InvocationExpressionSyntax call) {
            return false;
        }

        var name = call.ArgumentList.Arguments[0].Expression;
        return FixRebind.BindsTo(model, name, caught, context.CancellationToken)
            && model.GetOperation(call, context.CancellationToken) is IInvocationOperation bound
            && HasExceptionArgument(bound)
            && bound.Arguments.Any(candidate => candidate.Parameter?.Name == "exception"
                && candidate.Value.Syntax.Span == name.Span
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
