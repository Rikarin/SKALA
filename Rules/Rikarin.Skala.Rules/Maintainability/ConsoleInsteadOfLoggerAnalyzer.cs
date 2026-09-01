using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7093</c> — a write to the console from code that has a logger.
/// </summary>
/// <remarks>
///     A console write cannot be filtered, routed, structured or correlated, and in every hosted
///     environment — a container, a service, a function, a test host — it goes nowhere anybody looks.
///     <para>
///         ⚠
///         <b>
///             The rule does not decide whether this code is an application or a library, because
///             nothing in the tree can tell it.
///         </b> <c>LooseLoader</c> builds its compilation as
///         <c>OutputKind.DynamicallyLinkedLibrary</c>, so "this is a library" and "no project file was
///         loaded" are one observation — and loose is the mode Skala exists for. A rule keyed on
///         <c>OutputKind</c> would report every line of every console application analysed without its
///         project, which is <c>S106</c> turned into a false-positive engine.
///     </para>
///     <para>
///         What is decidable is the narrower fact the issue's own title names:
///         <b>
///             a logger is in scope
///             at this call site and the code wrote to the console anyway.
///         </b> A member or parameter typed
///         <c>ILogger</c>, <c>ILogger&lt;T&gt;</c> or <c>ILog</c> is present, so the routing question is
///         already answered for this code and answered differently two lines away. That is not a policy
///         judgement about the project's shape; it is a contradiction inside one method. An entry point
///         printing usage has no logger and is never reported, which is the correct outcome by
///         construction rather than by an exemption somebody has to maintain.
///     </para>
///     <para>
///         Report-only. Which level the line deserves is intent — <c>Debug</c>, <c>Information</c> and
///         <c>Error</c> are all plausible for the same string — and a console argument list is not
///         mechanically a message template. An edit that guessed would put the wrong severity on
///         somebody's alerting.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConsoleInsteadOfLoggerAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConsoleInsteadOfLogger);

    /// <summary>
    ///     What every logging library in use calls its logger interface.
    /// </summary>
    /// <remarks>
    ///     ⚠ Matched on the type's own name and not on a namespace, because the namespace is the part
    ///     that differs: <c>Microsoft.Extensions.Logging.ILogger</c>, <c>Serilog.ILogger</c>,
    ///     <c>NLog.ILogger</c> and <c>log4net.ILog</c> are the same concept under four roots, and a
    ///     compilation that has none of them referenced has nothing for this rule to find anyway.
    /// </remarks>
    static readonly HashSet<string> LoggerTypeNames = new(System.StringComparer.Ordinal) { "ILogger", "ILog" };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var console = start.Compilation.GetTypeByMetadataName("System.Console");
                if (console is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, console),
                    SyntaxKind.InvocationExpression
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol console) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { Name: "Write" or "WriteLine" } method
            || !WritesToTheConsole(context, invocation, method, console)) {
            return;
        }

        if (!LoggerInScope(context)) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                invocation.GetLocation(),
                "a logger is in scope here and this line goes to the console instead, where it cannot "
                + "be filtered, routed, structured or correlated"
            )
        );
    }

    /// <summary>
    ///     <c>Console.WriteLine(…)</c> directly, or through <c>Console.Out</c> / <c>Console.Error</c>.
    /// </summary>
    static bool WritesToTheConsole(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        INamedTypeSymbol console
    ) {
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, console)) {
            return true;
        }

        // ⚠ The receiver, not the type. `Console.Error.WriteLine` binds to `TextWriter.WriteLine`,
        // and a `TextWriter` the caller was handed is not the console — writing to one of those is
        // the routing the rule is asking for rather than the finding.
        return invocation.Expression is MemberAccessExpressionSyntax { Expression: { } receiver }
            && context.SemanticModel.GetSymbolInfo(receiver, context.CancellationToken).Symbol
            is IPropertySymbol property
            && SymbolEqualityComparer.Default.Equals(property.ContainingType, console);
    }

    /// <summary>
    ///     A member of the enclosing type, or a parameter of the enclosing method, that is a logger.
    /// </summary>
    static bool LoggerInScope(SyntaxNodeAnalysisContext context) {
        for (var symbol = context.ContainingSymbol; symbol is not null; symbol = symbol.ContainingSymbol) {
            if (symbol is IMethodSymbol method) {
                foreach (var parameter in method.Parameters) {
                    if (IsLogger(parameter.Type)) {
                        return true;
                    }
                }
            }

            if (symbol is not INamedTypeSymbol type) {
                continue;
            }

            for (var current = type; current is not null; current = current.BaseType) {
                foreach (var member in current.GetMembers()) {
                    if (member switch {
                            IFieldSymbol field => IsLogger(field.Type),
                            IPropertySymbol property => IsLogger(property.Type),
                            _ => false
                        }) {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    static bool IsLogger(ITypeSymbol type) {
        if (LoggerTypeNames.Contains(type.Name)) {
            return true;
        }

        foreach (var implemented in type.AllInterfaces) {
            if (LoggerTypeNames.Contains(implemented.Name)) {
                return true;
            }
        }

        return false;
    }
}
