using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Performance;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2082</c> — one element written twice with nothing reading it in between.
/// </summary>
/// <remarks>
///     <para>
///         A block of <c>map[…] = …;</c> statements is how a table gets built by hand, and it is
///         exactly where one key gets written twice: the first value is computed, stored, and thrown
///         away by the next statement, and nothing in the compiler notices because both writes are
///         legal.
///     </para>
///     <para>
///         ⚠
///         <b>
///             Roslyn's <c>AnalyzeDataFlow</c> answers questions about variables, not about indexed
///             elements
///         </b>, so there is no dataflow to lean on and this rule does not pretend to have
///         any. It reports only a <em>contiguous run of element writes to one collection</em>: the
///         moment a statement that is not such a write appears between the two, the finding is
///         withdrawn. That is not the whole of the defect and it is the part that can be proved
///         without a lattice.
///     </para>
///     <para>
///         ⚠ The run is additionally required to be <em>opaque-free</em>. An invocation, an object
///         creation, an <c>await</c>, a lambda, a nested assignment, a <c>++</c> or a <c>ref</c>
///         argument anywhere in an intervening write, and any mention of the collection itself on the
///         right of one, ends the run — because each of those is a place the first value could be
///         read through an alias the analyzer cannot see.
///     </para>
///     <para>
///         ⚠ <c>ConcurrentDictionary</c> is not in the receiver table and that is deliberate rather
///         than an omission: the whole point of the type is that another thread may read between the
///         two writes, so "nothing read it" is not something this analysis is entitled to say there.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OverwrittenElementAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.OverwrittenCollectionElement);

    /// <summary>
    ///     ⚠ A closed table, because an indexer is a method somebody wrote. A user type's setter may
    ///     accumulate, log or dispatch, and two writes to one index there are two events rather than
    ///     one lost value. Arrays are handled separately, by type kind.
    /// </summary>
    static readonly string[] Collections = [
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var collections = CollectionShape.Resolve(start.Compilation, Collections);
                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, collections),
                    SyntaxKind.Block,
                    SyntaxKind.SwitchSection
                );
            }
        );
    }

    sealed record Write(ElementAccessExpressionSyntax Access, ExpressionSyntax Key, ExpressionSyntax Value);

    static void Analyze(SyntaxNodeAnalysisContext context, List<INamedTypeSymbol> collections) {
        var statements = context.Node switch {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => default
        };

        if (statements.Count < 2) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        var writes = new Write?[statements.Count];
        for (var i = 0; i < statements.Count; i++) {
            writes[i] = Read(statements[i]);
        }

        for (var i = 0; i < writes.Length - 1; i++) {
            if (writes[i] is not { } first || !IsTracked(model, first, collections, cancellation)) {
                continue;
            }

            for (var j = i + 1; j < writes.Length; j++) {
                if (writes[j] is not { } next
                    || !CollectionShape.SameStorage(
                        model,
                        first.Access.Expression,
                        next.Access.Expression,
                        cancellation
                    )
                    || !IsOpaqueFree(next.Value)
                    || !IsOpaqueFree(next.Key)
                    || Mentions(model, next.Value, first.Access.Expression, cancellation)) {
                    break;
                }

                if (SameKey(model, first.Key, next.Key, cancellation)) {
                    Report(context, first, next);
                    break;
                }

                if (!DifferentKey(model, first.Key, next.Key, cancellation)) {
                    break;
                }
            }
        }
    }

    static void Report(SyntaxNodeAnalysisContext context, Write first, Write next) {
        var line = next.Access.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                first.Access.GetLocation(),
                "`"
                + first.Access
                + "` is written again on line "
                + line.ToString(CultureInfo.InvariantCulture)
                + " with nothing reading it in between, so this value is computed and discarded"
            )
        );
    }

    /// <summary>A statement of the form <c>c[k] = v;</c>, or null.</summary>
    static Write? Read(StatementSyntax statement) {
        if (statement is not ExpressionStatementSyntax {
                Expression:
                AssignmentExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } access
                } assignment
            }
            || access.ArgumentList.Arguments[0] is not {
                NameColon: null,
                RefKindKeyword.RawKind: (int)SyntaxKind.None
            } argument
            || !CallShape.IsPlainNamePath(access.Expression)) {
            return null;
        }

        return new(access, argument.Expression, assignment.Right);
    }

    static bool IsTracked(
        SemanticModel model,
        Write write,
        List<INamedTypeSymbol> collections,
        CancellationToken cancellation
    ) {
        if (model.GetTypeInfo(write.Access.Expression, cancellation).Type is not { } type
            || type.TypeKind == TypeKind.Error) {
            return false;
        }

        // ⚠ An array's element write is a store and nothing else, which is the whole premise. A
        // named type has to be in the table, because an indexer is a method somebody wrote.
        return type.TypeKind == TypeKind.Array || CollectionShape.Contains(collections, type);
    }

    /// <summary>
    ///     The two keys certainly denote one entry: equal constants, or one storage read twice.
    /// </summary>
    static bool SameKey(
        SemanticModel model,
        ExpressionSyntax left,
        ExpressionSyntax right,
        CancellationToken cancellation
    ) {
        var a = CollectionShape.ConstantKey(model, left, cancellation);
        if (a is not null) {
            return string.Equals(a, CollectionShape.ConstantKey(model, right, cancellation), StringComparison.Ordinal);
        }

        return CollectionShape.SameStorage(model, left, right, cancellation);
    }

    /// <summary>
    ///     ⚠ The two keys certainly denote different entries, which is a stronger claim than "not the
    ///     same". Only two constants answer it: a comparer can make two distinct-looking values one
    ///     key, and two different variables can hold one value.
    /// </summary>
    static bool DifferentKey(
        SemanticModel model,
        ExpressionSyntax left,
        ExpressionSyntax right,
        CancellationToken cancellation
    ) {
        var a = CollectionShape.ConstantKey(model, left, cancellation);
        var b = CollectionShape.ConstantKey(model, right, cancellation);
        return a is not null && b is not null && !string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Whether an expression contains nothing that could reach the collection behind the
    ///     analyzer's back.
    /// </summary>
    static bool IsOpaqueFree(ExpressionSyntax expression) {
        foreach (var node in expression.DescendantNodesAndSelf()) {
            switch (node) {
                case InvocationExpressionSyntax:
                case ObjectCreationExpressionSyntax:
                case ImplicitObjectCreationExpressionSyntax:
                case AwaitExpressionSyntax:
                case AssignmentExpressionSyntax:
                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                case PrefixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression
                }:
                case PostfixUnaryExpressionSyntax {
                    RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression
                }:
                case ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None }:
                    return false;
            }
        }

        return true;
    }

    /// <summary>Whether an expression reads the collection anywhere inside it.</summary>
    static bool Mentions(
        SemanticModel model,
        ExpressionSyntax expression,
        ExpressionSyntax collection,
        CancellationToken cancellation
    ) {
        foreach (var node in expression.DescendantNodesAndSelf()) {
            if (node is ExpressionSyntax candidate
                && CollectionShape.SameStorage(model, candidate, collection, cancellation)) {
                return true;
            }
        }

        return false;
    }
}
