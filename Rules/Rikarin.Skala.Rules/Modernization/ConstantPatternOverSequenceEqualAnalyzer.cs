using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;
using System.Threading;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1130</c> — <c>span.SequenceEqual("abc")</c> where C# 11 spells <c>span is "abc"</c>.
/// </summary>
/// <remarks>
///     <para>
///         C# 11 lets a <c>ReadOnlySpan&lt;char&gt;</c> or <c>Span&lt;char&gt;</c> be matched against a
///         constant string, and lowers that pattern to the same <c>MemoryExtensions.SequenceEqual</c>
///         the source was already calling. The call and the pattern are one program; only the pattern
///         composes, which is the whole finding — it joins an <c>or</c>, goes into a <c>switch</c> arm,
///         and negates as <c>is not</c> without a <c>!</c> in front of a long receiver.
///     </para>
///     <para>
///         ⚠
///         <b>
///             The two spellings agree on every input, and that was measured rather than reasoned
///             about.
///         </b> Fourteen inputs were compiled and run — an exact match, longer and shorter spans,
///         an empty span, <c>default(ReadOnlySpan&lt;char&gt;)</c>, a span over a null string, two
///         sliced spans, a case difference, and a <c>Span&lt;char&gt;</c> from <c>stackalloc</c> — and
///         the pattern and the call returned the same <c>bool</c> for all of them. The empty constant
///         was checked separately, because it is the one that could have split <c>default</c> from
///         <c>""</c>: both are <c>true</c> for a default span. That is why the fix is safe.
///     </para>
///     <para>
///         ⚠ <b><c>Enumerable.SequenceEqual</c> is a different method and does <em>not</em> agree.</b>
///         On a <c>string</c> receiver the call is LINQ over <c>IEnumerable&lt;char&gt;</c>, and a null
///         receiver throws <c>ArgumentNullException</c> where <c>s is "abc"</c> returns <c>false</c> —
///         also compiled and run. Requiring the resolved symbol to be
///         <c>System.MemoryExtensions.SequenceEqual</c> is what keeps that shape out.
///     </para>
///     <para>
///         ⚠ <b>The receiver's own static type must already be a character span.</b>
///         <c>char[] a; a.SequenceEqual("abc")</c> reaches the same method through an implicit span
///         conversion, but <c>a is "abc"</c> is <c>CS0029</c> — a pattern has no conversion step. The
///         guard reads <see cref="TypeInfo.Type" /> and never <see cref="TypeInfo.ConvertedType" />.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantPatternOverSequenceEqualAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.ConstantPatternOverSequenceEqual);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.ConstantPatternOverSequenceEqual);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ CS8936 below C# 11: "Feature 'pattern matching ReadOnly/Span<char> on constant
                // string' is not available". Confirmed by compiling it, not read off a table.
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ContainsDiagnostics || Name(invocation) != "SequenceEqual") {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
            return;
        }

        // ⚠ The reduced form is what an extension call binds to; the unreduced one is where the
        // declaring type is read. Anything else called `SequenceEqual` — LINQ's, or one somebody
        // wrote — has no defined relationship to `is` at all.
        //
        // ⚠ **A parameter-count check stood here and was removed as dead.** It was meant to exclude
        // the `IEqualityComparer<T>` overload, and it never once did: `Operands` already requires
        // one argument in the extension spelling and two in the static one, so a comparer call is
        // turned away a step later in both. Deleting the check turned no fixture red, and no
        // fixture that reaches it can be written — omitting the optional comparer binds to the
        // two-parameter overload rather than defaulting the three-parameter one. `Operands`' arity
        // is what carries the concept, and it has its own sabotage.
        var declaration = (method.ReducedFrom ?? method).OriginalDefinition;
        if (declaration.ContainingType?.ToDisplayString() != "System.MemoryExtensions") {
            return;
        }

        if (Operands(invocation, method) is not ({ } receiver, { } argument)) {
            return;
        }

        // ⚠ `Type`, never `ConvertedType`: a `char[]` reaches this method by an implicit span
        // conversion and `a is "abc"` is CS0029, because a pattern has no conversion step.
        //
        // ⚠ **An element-type check stood here and was removed as dead.** It required
        // `TypeArguments[0]` to be `char`, and nothing can reach it: both parameters of
        // `SequenceEqual` are `ReadOnlySpan<T>` of the same `T`, and a string constant converts to
        // `ReadOnlySpan<char>` and to nothing else — so requiring the argument to be a constant
        // string already fixes `T` at `char`. Deleting it turned no fixture red, and the byte-span
        // fixture that was written to reach it is declined by the constant check instead, which is
        // what its comment now says.
        if (model.GetTypeInfo(receiver, cancellation).Type is not INamedTypeSymbol {
                Name: "Span" or "ReadOnlySpan",
                ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true }
            }) {
            return;
        }

        if (ConstantText(model, argument, cancellation) is not { } constant || !IsPrimary(receiver)) {
            return;
        }

        // `!span.SequenceEqual("abc")` is one edit into `span is not "abc"`, not a `!` left standing
        // in front of a pattern.
        var negated = false;
        ExpressionSyntax reported = invocation;
        if (invocation.Parent is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not) {
            negated = true;
            reported = not;
        }

        if (!PatternSafety.IsPatternSafeContext(reported) || RewriteGuards.ContainsCommentOrDirective(reported)) {
            return;
        }

        var replacement = receiver + (negated ? " is not " : " is ") + constant;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                reported.GetLocation(),
                FixEdits.Pack((reported.Span, replacement)),
                "The span is compared to a constant: `" + RewriteGuards.Trim(replacement) + "`"
            )
        );
    }

    static string? Name(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch {
            MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access =>
                access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null
        };

    /// <summary>
    ///     The span being tested and the constant it is tested against, in either spelling.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ReducedFrom</c> is what separates them. Called as an extension the span is the
    ///     receiver of the member access and the constant is the only argument; called statically —
    ///     <c>MemoryExtensions.SequenceEqual(x, "abc")</c> — both are arguments, and the span is the
    ///     first. Reading the argument list alone gets the static form's operands backwards.
    /// </remarks>
    static (ExpressionSyntax Receiver, ExpressionSyntax Argument)? Operands(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method
    ) {
        var arguments = invocation.ArgumentList.Arguments;
        if (method.ReducedFrom is not null) {
            return invocation.Expression is MemberAccessExpressionSyntax access && arguments.Count == 1
                ? (access.Expression, arguments[0].Expression)
                : null;
        }

        return arguments.Count == 2 ? (arguments[0].Expression, arguments[1].Expression) : null;
    }

    /// <summary>The source text of a compile-time non-null string, or null when there is not one.</summary>
    /// <remarks>
    ///     ⚠ <b>The constant is re-spelled from its own source</b>, so a <c>const string</c> argument
    ///     stays a <c>const string</c> — a constant pattern accepts one — and a verbatim or raw literal
    ///     keeps its escaping rather than being rebuilt from the value.
    ///     <para>
    ///         ⚠ <c>"abc".AsSpan()</c> is unwrapped to <c>"abc"</c>: the pattern takes the string, not
    ///         a span over it. Only the no-argument overload, because <c>AsSpan(1, 2)</c> is a slice and
    ///         its value is not the literal.
    ///     </para>
    ///     <para>
    ///         ⚠ A <c>null</c> constant is declined by the type pattern below rather than by a test —
    ///         there is no <c>is null</c> for a span, so it has no rewrite at all.
    ///     </para>
    /// </remarks>
    static string? ConstantText(SemanticModel model, ExpressionSyntax argument, CancellationToken cancellation) {
        var expression = PatternSafety.Unwrap(argument);
        if (expression is InvocationExpressionSyntax {
                ArgumentList.Arguments.Count: 0,
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Name.Identifier.ValueText: "AsSpan"
                } asSpan
            }) {
            expression = PatternSafety.Unwrap(asSpan.Expression);
        }

        return model.GetConstantValue(expression, cancellation) is { HasValue: true, Value: string }
            ? expression.ToString()
            : null;
    }

    /// <summary>
    ///     ⚠ Whether the span may be moved to the left of an <c>is</c> without gaining parentheses.
    /// </summary>
    /// <remarks>
    ///     This bites only the static spelling. A receiver of a member access is already a primary
    ///     expression, but <c>MemoryExtensions.SequenceEqual(a ? b : c, "x")</c> puts an arbitrary
    ///     expression in argument position, where every precedence is legal and most are not legal to
    ///     the left of a relational operator.
    /// </remarks>
    static bool IsPrimary(ExpressionSyntax expression) =>
        expression is IdentifierNameSyntax
            or ThisExpressionSyntax
            or ParenthesizedExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression };
}
