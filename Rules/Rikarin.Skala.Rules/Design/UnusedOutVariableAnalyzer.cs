using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Design;

/// <summary><c>SK6040</c> — an <c>out</c> argument declares a variable nothing ever mentions again.</summary>
/// <remarks>
///     ⚠ This is one of the nine inspections issue #121 grouped into "the local declaration is never
///     used", and the only one that ships. It is the shape whose repair is mechanical and cannot change
///     what the program does: the callee writes the argument either way, so replacing the declaration
///     with <c>_</c> removes a name and nothing else. Deleting an unread ordinary local cannot promise
///     that — <c>var response = Send();</c> is unread and deleting it stops the request — and
///     <c>UnusedLocalFunction</c> is already CS8321, so re-implementing it would be noise.
///     <para>
///         ⚠ The analysis is over operations rather than syntax because "is this name read" is a question
///         about a symbol: an identifier that spells the variable's name may be a field, a type or a
///         parameter. It is <em>also</em> over tokens, as a second and deliberately redundant guard —
///         <c>nameof(x)</c> and anything else the operation walk might not surface withdraws the finding,
///         because being wrong in that direction costs one finding and being wrong in the other costs a
///         build.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedOutVariableAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.UnusedOutVariable);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilation => {
                if (!SkalaRule.MeetsLanguageVersion(compilation.Compilation, "7.0")) {
                    return;
                }

                compilation.RegisterOperationBlockStartAction(Start);
            }
        );
    }

    /// <summary>
    ///     One member's worth of state: what was declared in an <c>out</c> position, and what was named.
    /// </summary>
    /// <remarks>
    ///     ⚠ Both callbacks run concurrently for different operations in the same block, so the two
    ///     collections are guarded. The state is per-block-start and therefore per-member, which is also
    ///     what makes the rule cacheable per file: an <c>out</c> variable's scope never leaves the member
    ///     that declares it.
    /// </remarks>
    static void Start(OperationBlockStartAnalysisContext context) {
        var gate = new object();
        var declared = new List<(ILocalSymbol Symbol, DeclarationExpressionSyntax Syntax)>();
        var referenced = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

        context.RegisterOperationAction(
            operation => {
                var reference = (ILocalReferenceOperation)operation.Operation;

                // ⚠ `IsDeclaration` is the declaration site itself — `out var x` produces a local
                // reference for `x` as well as the declaration expression around it. Counting it as
                // a use would make the rule report nothing at all, silently.
                if (reference.IsDeclaration) {
                    return;
                }

                lock (gate) {
                    referenced.Add(reference.Local);
                }
            },
            OperationKind.LocalReference
        );

        context.RegisterOperationAction(
            operation => {
                if (operation.Operation is not IDeclarationExpressionOperation {
                        Syntax: DeclarationExpressionSyntax syntax,
                        Expression: ILocalReferenceOperation { IsDeclaration: true } local
                    }) {
                    return;
                }

                // The `out` keyword is what makes the repair safe: the value is produced by the
                // callee whether or not the caller keeps it. A declaration expression in any other
                // position — a `is T x` pattern, a deconstruction — is a different question.
                if (syntax.Parent is not ArgumentSyntax argument
                    || !argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) {
                    return;
                }

                // ⚠ An explicitly typed `out` declaration takes part in method type inference, so
                // `M(out int x)` can be what fixes `T` — and `M(out _)` is then CS8183, "cannot
                // infer the type of implicitly-typed discard". Deciding whether inference still
                // succeeds means re-running it; declining whenever the call is to a generic method
                // that was not given its type arguments in source is cheaper and only ever costs
                // findings.
                if (InfersItsTypeArguments(operation.Operation.Parent)) {
                    return;
                }

                lock (gate) {
                    declared.Add((local.Local, syntax));
                }
            },
            OperationKind.DeclarationExpression
        );

        context.RegisterOperationBlockEndAction(end => {
                if (declared.Count == 0) {
                    return;
                }

                // ⚠ Disabled text is trivia. A reference inside `#if DEBUG` is invisible to the
                // operation tree *and* to the token scan below, and under the other configuration
                // the variable is read — so a member carrying a directive is not answerable here.
                foreach (var block in end.OperationBlocks) {
                    if (block.Syntax.ContainsDirectives) {
                        return;
                    }
                }

                foreach (var (symbol, syntax) in declared) {
                    if (referenced.Contains(symbol) || IsNamedAnywhere(end.OperationBlocks, symbol.Name, syntax)) {
                        continue;
                    }

                    end.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptor,
                            syntax.GetLocation(),
                            FixEdits.Pack((syntax.Span, "_")),
                            "`"
                            + symbol.Name
                            + "` receives an `out` argument and is never read; write `out _` so the "
                            + "name does not enter scope"
                        )
                    );
                }
            }
        );
    }

    /// <summary>
    ///     Whether the call around this argument is to a generic method with no type arguments in source.
    /// </summary>
    /// <remarks>
    ///     ⚠ The symbol cannot answer this on its own. <c>IArgumentOperation.Parameter</c> belongs to the
    ///     <em>constructed</em> method, so its type is already <c>string</c> and never <c>T</c> — a guard
    ///     reading the parameter type would be dead code. What has to be asked is whether the source
    ///     wrote the type arguments: if it did not, inference ran, and an explicitly typed <c>out</c>
    ///     declaration is one of the things inference is allowed to read.
    /// </remarks>
    static bool InfersItsTypeArguments(IOperation? argument) {
        if (argument?.Parent is not IInvocationOperation { TargetMethod.IsGenericMethod: true } invocation) {
            return false;
        }

        var name = invocation.Syntax switch {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } => access.Name,
            InvocationExpressionSyntax { Expression: SimpleNameSyntax simple } => simple,
            _ => null
        };

        return name is not GenericNameSyntax;
    }

    /// <summary>
    ///     The redundant guard: does any token in the member spell this name, other than the declaration?
    /// </summary>
    /// <remarks>
    ///     ⚠ Deliberately weaker than the operation walk and deliberately kept anyway. It withdraws the
    ///     finding for <c>nameof(x)</c> and for anything else a future Roslyn stops surfacing as a local
    ///     reference, at the cost of also withdrawing it when an unrelated member or type happens to
    ///     share the variable's name. One missed finding against one edit that does not compile is not a
    ///     close call.
    /// </remarks>
    static bool IsNamedAnywhere(
        ImmutableArray<IOperation> blocks,
        string name,
        DeclarationExpressionSyntax declaration
    ) {
        foreach (var block in blocks) {
            foreach (var token in block.Syntax.DescendantTokens()) {
                if (token.IsKind(SyntaxKind.IdentifierToken)
                    && string.Equals(token.ValueText, name, System.StringComparison.Ordinal)
                    && !declaration.Span.Contains(token.Span)) {
                    return true;
                }
            }
        }

        return false;
    }
}
