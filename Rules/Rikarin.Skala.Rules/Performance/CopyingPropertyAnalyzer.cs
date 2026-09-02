using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Performance;

/// <summary>
///     <c>SK4040</c> — a property whose getter allocates a fresh copy of a collection it could have
///     returned unchanged.
/// </summary>
/// <remarks>
///     <para>
///         <c>public IReadOnlyList&lt;T&gt; Items =&gt; items.ToList();</c> is an <c>O(n)</c> allocation
///         at every access, and nothing at the call site says so. Property syntax reads as a field
///         read, so callers put it inside loops and inside conditions, and the cost multiplies
///         somewhere nobody is looking at this declaration.
///     </para>
///     <para>
///         ⚠ <b>A deliberate defensive copy has this shape too, and nothing tells them apart.</b>
///         That is why the fix is <c>fixIsSafe: false</c> and why the rule is a suggestion
///         rather than a warning: the finding is "callers cannot see this cost", which is true of the
///         deliberate copy too, and the answer to it may be to keep the copy and move it behind a
///         method whose parentheses admit the work.
///     </para>
///     <para>
///         ⚠ <b>The rule is silent where the materialization also converts.</b> Only a copy the
///         property could have skipped entirely — where the source's own type already converts to the
///         property's by identity or by reference — is reported. <c>int[] Items =&gt; list.ToArray();</c>
///         is doing conversion work as well as copying, there is no edit that keeps the declared type,
///         and a finding with no available answer is one a reader has to argue with rather than act on.
///     </para>
///     <para>
///         ⚠ <b>Distinct from <c>CA1819</c>, which was probed rather than assumed.</b> <c>CA1819</c>
///         asks about the property's <em>type</em> — it reports <c>int[] P =&gt; field;</c>, which
///         copies nothing, and it says nothing about <c>IReadOnlyList&lt;T&gt; P =&gt; xs.ToList();</c>,
///         which is the whole of this concept. It is also <c>enabledByDefault: false</c>, so it is
///         silent until a repository raises <c>AnalysisMode</c>. The two overlap on
///         <c>T[] P =&gt; array.ToArray();</c> and disagree about why.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CopyingPropertyAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.CopyingProperty);

    /// <summary>
    ///     ⚠ The <c>System.Linq.Enumerable</c> members that always allocate a new collection.
    /// </summary>
    /// <remarks>
    ///     Matched by symbol rather than by name — an extension called <c>ToList</c> on somebody else's
    ///     static class is a different method with different cost, and a rule that reads the identifier
    ///     would report it anyway.
    /// </remarks>
    static readonly string[] Materializers = ["ToList", "ToArray", "ToHashSet", "ToDictionary", "ToLookup"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                var enumerable = start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable");
                if (enumerable is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, enumerable),
                    SyntaxKind.PropertyDeclaration
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerable) {
        var property = (PropertyDeclarationSyntax)context.Node;

        // ⚠ A property with a setter is still reported on its getter — the cost is per read either
        // way — but a getter that does anything besides produce the value is not this shape.
        if (SoleGetterExpression(property) is not { } body) {
            return;
        }

        if (Materialized(body, context.SemanticModel, enumerable, context.CancellationToken) is not { } source) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The source has to be readable twice for free, because the fix reads it where the whole
        // call used to be. A pipeline — `items.Where(p).ToList()` — is a computed property doing
        // work the parentheses of the operators already admit to, and is deliberately not this rule.
        if (!CallShape.IsPlainNamePath(source)) {
            return;
        }

        if (model.GetDeclaredSymbol(property, cancellation) is not { } declared
            || model.GetTypeInfo(source, cancellation).Type is not { } sourceType) {
            return;
        }

        // ⚠ Only a copy the declaration could have skipped. Where the materialization also changes
        // the type there is no edit that keeps the property's signature, and doc 08's bar is a rule
        // that can be acted on rather than one that is merely right.
        var conversion = model.Compilation.ClassifyCommonConversion(sourceType, declared.Type);
        // ⚠ `IsIdentity || IsReference` is the whole test, and it is what excludes boxing and every
        // user-defined conversion without naming them: neither is a reference conversion, so a
        // struct-to-interface source and an implicit operator both fall out here.
        if (!conversion.Exists
            || !conversion.IsImplicit
            || conversion.IsUserDefined
            || !(conversion.IsIdentity || conversion.IsReference)) {
            return;
        }

        var span = TextSpan.FromBounds(body.SpanStart, body.Span.End);

        // ⚠ The whole expression is replaced by the source's text, so a comment anywhere inside the
        // call is text the fix would delete. `SpanContainsComment` over the *node* rather than over
        // the line, because trivia above a declaration is not inside it (#302).
        if (RewriteGuards.ContainsCommentOrDirective(body)) {
            return;
        }

        var text = body.SyntaxTree.GetText(cancellation).ToString(source.Span);

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(body.SyntaxTree, span),
                FixEdits.Pack((span, text)),
                "`"
                + declared.Name
                + "` allocates a copy of `"
                + RewriteGuards.Trim(text)
                + "` at every read, and the property's type already accepts the source unchanged"
            )
        );
    }

    /// <summary>
    ///     The single expression a property's getter produces, or <c>null</c> when it does more.
    /// </summary>
    /// <remarks>
    ///     ⚠ Three spellings, one shape: <c>=&gt; e;</c>, <c>get =&gt; e;</c> and
    ///     <c>get { return e; }</c>. A getter with a second statement is computing something, and the
    ///     rule has no claim on it. An accessor list without a getter — a write-only property — has no
    ///     read to be expensive.
    /// </remarks>
    static ExpressionSyntax? SoleGetterExpression(PropertyDeclarationSyntax property) {
        if (property.ExpressionBody is { Expression: { } arrow }) {
            return arrow;
        }

        if (property.AccessorList is null) {
            return null;
        }

        foreach (var accessor in property.AccessorList.Accessors) {
            if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) {
                continue;
            }

            if (accessor.ExpressionBody is { Expression: { } expression }) {
                return expression;
            }

            // ⚠ Written out rather than as a list pattern: this assembly targets netstandard2.0 and a
            // list pattern there is CS0518 — the pattern needs `System.Index`, which is not in the
            // reference set.
            return accessor.Body is { Statements.Count: 1 }
                && accessor.Body.Statements[0] is ReturnStatementSyntax { Expression: { } returned }
                    ? returned
                    : null;
        }

        return null;
    }

    /// <summary>
    ///     The source of a call that always allocates a new collection, or <c>null</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Two families, and both are bound rather than name-matched. The LINQ materializers are
    ///     required to be <c>System.Linq.Enumerable</c>'s, so a hand-written <c>ToList</c> extension is
    ///     not this rule's business. A copy constructor is required to be a constructor of a type that
    ///     implements <c>IEnumerable</c> taking one sequence argument, which is what
    ///     <c>new List&lt;T&gt;(xs)</c> and <c>new HashSet&lt;T&gt;(xs)</c> are and what
    ///     <c>new List&lt;T&gt;(capacity)</c> is not.
    /// </remarks>
    static ExpressionSyntax? Materialized(
        ExpressionSyntax body,
        SemanticModel model,
        INamedTypeSymbol enumerable,
        System.Threading.CancellationToken cancellation
    ) {
        switch (body) {
            case InvocationExpressionSyntax {
                Expression:
                MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            } invocation
                when Array.IndexOf(Materializers, access.Name.Identifier.ValueText) >= 0
                && invocation.ArgumentList.Arguments.Count == 0: {
                if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method) {
                    return null;
                }

                var original = (method.ReducedFrom ?? method).OriginalDefinition;
                return SymbolEqualityComparer.Default.Equals(original.ContainingType, enumerable)
                    ? access.Expression
                    : null;
            }

            // ⚠ `BaseObjectCreationExpressionSyntax`, so the target-typed `new(items)` spelling is the
            // same finding as `new List<T>(items)`. Matching only the explicit form would make the
            // rule depend on how the author wrote the type name.
            case BaseObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } creation: {
                var argument = creation.ArgumentList!.Arguments[0].Expression;
                if (model.GetSymbolInfo(creation, cancellation).Symbol is not IMethodSymbol constructor
                    || constructor.Parameters.Length != 1) {
                    return null;
                }

                // ⚠ `new List<T>(capacity)` allocates too, and allocates *nothing that was copied*.
                // The discriminator is the parameter's type: a sequence means the constructor walks
                // it, an `int` means it reserves room.
                return Sequence(constructor.Parameters[0].Type) && Sequence(constructor.ContainingType)
                    ? argument
                    : null;
            }

            default:
                return null;
        }
    }

    static bool Sequence(ITypeSymbol type) {
        if (type.SpecialType is SpecialType.System_Collections_IEnumerable
            or SpecialType.System_Collections_Generic_IEnumerable_T) {
            return true;
        }

        foreach (var @interface in type.AllInterfaces) {
            if (@interface.SpecialType is SpecialType.System_Collections_IEnumerable
                or SpecialType.System_Collections_Generic_IEnumerable_T) {
                return true;
            }
        }

        return false;
    }
}
