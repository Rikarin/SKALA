using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1033</c> — <c>ContainsKey</c> followed by the indexer, or by <c>Add</c>, is one lookup
///     written as two.
/// </summary>
/// <remarks>
///     ⚠ Two shapes only, and the reason for stopping there is that they are the two the rewrite can be
///     <em>proved</em> on. <c>if (d.ContainsKey(k)) { var v = d[k]; … }</c> becomes
///     <c>if (d.TryGetValue(k, out var v)) { … }</c> and <c>if (!d.ContainsKey(k)) d[k] = v;</c> becomes
///     <c>d.TryAdd(k, v);</c>; both are the same program with one hash lookup instead of two. The
///     general form — an indexer read somewhere in a long body — needs the fix to invent a variable, place
///     its declaration and rewrite every use, and a rule whose fix is a refactor is a rule that ships
///     without one.
///     <para>
///         ⚠ The receiver is required to be <c>Dictionary&lt;K, V&gt;</c> exactly rather than
///         <c>IDictionary&lt;K, V&gt;</c>. <c>TryAdd</c> is not on the interface — it is an extension in
///         <c>CollectionExtensions</c> that a project may not have in scope — and on
///         <c>ConcurrentDictionary</c> the <c>ContainsKey</c>/indexer pair is a race the rewrite would
///         quietly change the shape of rather than a redundancy it removes.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DictionaryLookupAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.DictionaryDoubleLookup);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.DictionaryDoubleLookup);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (!SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    return;
                }

                var dictionary = start.Compilation
                    .GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
                if (dictionary is null) {
                    return;
                }

                start.RegisterSyntaxNodeAction(
                    context => Analyze(context, dictionary),
                    SyntaxKind.IfStatement
                );
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionary) {
        var statement = (IfStatementSyntax)context.Node;

        // ⚠ An `else` is not merely another shape. Both rewrites move a declaration into the
        // condition, where it is in scope for the `else` too but not definitely assigned there, so
        // a name the `else` already uses becomes CS0128 and a name it reads becomes CS0165. The
        // guard that would make it safe is a second scope analysis for a case that is rare.
        if (statement.Else is not null) {
            return;
        }

        var (negated, condition) = Unwrap(statement.Condition);
        if (condition is not InvocationExpressionSyntax {
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Name: IdentifierNameSyntax { Identifier.ValueText: "ContainsKey" }
                } access,
                ArgumentList.Arguments.Count: 1
            } invocation) {
            return;
        }

        var key = invocation.ArgumentList.Arguments[0].Expression;
        if (!IsStable(access.Expression) || !IsStable(key)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetTypeInfo(access.Expression, cancellation).Type is not INamedTypeSymbol receiver
            || !SymbolEqualityComparer.Default.Equals(receiver.OriginalDefinition, dictionary)) {
            return;
        }

        // ⚠ A pattern and an `out var` are both illegal inside an expression tree, and so is the
        // statement rewrite that produces them. CS8198 is not something a fix may hand an agent.
        if (NullComparison.InsideExpressionTree(model, statement, cancellation)) {
            return;
        }

        if (negated) {
            ReportTryAdd(context, statement, invocation, access, key, receiver);
        } else {
            ReportTryGetValue(context, statement, invocation, access, key, receiver);
        }
    }

    /// <summary>
    ///     <c>if (d.ContainsKey(k)) { var v = d[k]; … }</c> → <c>if (d.TryGetValue(k, out var v)) { … }</c>.
    /// </summary>
    static void ReportTryGetValue(
        SyntaxNodeAnalysisContext context,
        IfStatementSyntax statement,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        ExpressionSyntax key,
        INamedTypeSymbol receiver
    ) {
        if (statement.Statement is not BlockSyntax { Statements.Count: > 0 } block
            || block.Statements[0] is not LocalDeclarationStatementSyntax {
                UsingKeyword.RawKind: (int)SyntaxKind.None,
                Declaration: { Variables.Count: 1 } declaration
            } first
            || first.Modifiers.Count > 0) {
            return;
        }

        var declarator = declaration.Variables[0];
        if (declarator.Initializer?.Value is not ElementAccessExpressionSyntax {
                ArgumentList.Arguments.Count: 1
            } element) {
            return;
        }

        if (!RewriteGuards.Same(element.Expression, access.Expression)
            || !RewriteGuards.Same(element.ArgumentList.Arguments[0].Expression, key)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ `out var v` binds `v` to the dictionary's value type. `object v = d[k];` is legal today
        // and `out object v` is not, so an explicit type is admitted only when it is that type
        // exactly — and `var` is admitted because it already is.
        if (!declaration.Type.IsVar) {
            var declared = model.GetTypeInfo(declaration.Type, cancellation).Type;
            if (declared is null
                || !SymbolEqualityComparer.Default.Equals(declared, receiver.TypeArguments[1])) {
                return;
            }
        }

        // ⚠ C# scopes an `out var` declared in an `if` condition to the *enclosing block*, not to
        // the `if`, so the declaration moves one scope outwards and has to answer both halves of
        // the collision question: what is in scope here, and what a neighbouring scope of the same
        // member declares — the second being invisible to a lookup and still CS0136.
        var name = declarator.Identifier.ValueText;
        if (RewriteGuards.WouldCollide(model, statement.SpanStart, name, cancellation)
            || RewriteGuards.DeclaredElsewhereInMember(statement, name)) {
            return;
        }

        // ⚠ The node question, deliberately: the fix's second edit is `LineSpanOf(first)`, so the
        // comment above the declaration really is inside the text this deletes.
        if (RewriteGuards.ContainsCommentOrDirectiveAroundTheDeclaration(first)) {
            return;
        }

        var declaredType = declaration.Type.IsVar ? "var" : declaration.Type.ToString();
        var replacement = access.Expression + ".TryGetValue(" + key + ", out " + declaredType + " " + name + ")";

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, invocation.Span),
                FixEdits.Pack(
                    (invocation.Span, replacement),
                    (RewriteGuards.LineSpanOf(first), string.Empty)
                ),
                "`ContainsKey` and the indexer hash the key twice: `"
                + RewriteGuards.Trim(replacement)
                + "`"
            )
        );
    }

    /// <summary>
    ///     <c>if (!d.ContainsKey(k)) d[k] = v;</c> and <c>… d.Add(k, v);</c> → <c>d.TryAdd(k, v);</c>.
    /// </summary>
    static void ReportTryAdd(
        SyntaxNodeAnalysisContext context,
        IfStatementSyntax statement,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        ExpressionSyntax key,
        INamedTypeSymbol receiver
    ) {
        if (!HasTryAdd(receiver)) {
            return;
        }

        var body = statement.Statement switch {
            BlockSyntax { Statements.Count: 1 } block => block.Statements[0],
            ExpressionStatementSyntax single => single,
            _ => null
        };

        if (body is not ExpressionStatementSyntax expression) {
            return;
        }

        var value = ValueAssigned(expression.Expression, access.Expression, key);
        if (value is null) {
            return;
        }

        // ⚠ The value is the trap in this shape, and it is invisible until it is written down.
        // `if (!d.ContainsKey(k)) d[k] = Build();` calls `Build()` only when the key is absent.
        // `d.TryAdd(k, Build())` calls it **every time** — C# evaluates the arguments before the
        // call — and then throws the result away when the key was present. On Vixen that is
        // `mesh.AddPosition(…)`, which mutates the mesh, and `edited.ToMeshData(…)`, which builds
        // one; the first is a behaviour change and the second is an allocation added to the common
        // path. So the value has to be something already computed.
        if (!IsStable(value)) {
            return;
        }

        if (RewriteGuards.ContainsCommentOrDirectiveWithinTheEdit(statement.SyntaxTree, statement.Span)) {
            return;
        }

        var replacement = access.Expression + ".TryAdd(" + key + ", " + value + ");";
        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(statement.SyntaxTree, invocation.Span),
                FixEdits.Pack((statement.Span, replacement)),
                "`!ContainsKey` then a write hashes the key twice: `"
                + RewriteGuards.Trim(replacement)
                + "`"
            )
        );
    }

    /// <summary>
    ///     The value written by <c>d[k] = v</c> or <c>d.Add(k, v)</c> on the same receiver and key.
    /// </summary>
    static ExpressionSyntax? ValueAssigned(
        ExpressionSyntax expression,
        ExpressionSyntax receiver,
        ExpressionSyntax key
    ) {
        switch (expression) {
            case AssignmentExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                Left: ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } element
            } assignment
                when RewriteGuards.Same(element.Expression, receiver)
                && RewriteGuards.Same(element.ArgumentList.Arguments[0].Expression, key):
                return assignment.Right;

            case InvocationExpressionSyntax {
                Expression:
                MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Name: IdentifierNameSyntax { Identifier.ValueText: "Add" }
                } add,
                ArgumentList.Arguments: { Count: 2 } arguments
            }
                when RewriteGuards.Same(add.Expression, receiver)
                && RewriteGuards.Same(arguments[0].Expression, key)
                && arguments[0].NameColon is null
                && arguments[1].NameColon is null
                && arguments[1].RefKindKeyword.IsKind(SyntaxKind.None):
                return arguments[1].Expression;

            default:
                return null;
        }
    }

    static bool HasTryAdd(INamedTypeSymbol receiver) {
        foreach (var member in receiver.GetMembers("TryAdd")) {
            if (member is IMethodSymbol { IsStatic: false, Parameters.Length: 2 }) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     ⚠ Both rewrites evaluate the receiver and the key once where the original evaluated them
    ///     twice, so both have to be expressions for which that is not observable.
    /// </summary>
    static bool IsStable(ExpressionSyntax expression) =>
        RewriteGuards.IsPlainNamePath(expression) || expression is LiteralExpressionSyntax;

    /// <summary>The condition with any <c>!</c> and parentheses stripped, and whether it had one.</summary>
    static (bool Negated, ExpressionSyntax Condition) Unwrap(ExpressionSyntax condition) {
        var negated = false;
        while (true) {
            switch (condition) {
                case ParenthesizedExpressionSyntax parenthesized:
                    condition = parenthesized.Expression;
                    continue;

                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } unary:
                    negated = !negated;
                    condition = unary.Operand;
                    continue;

                default:
                    return (negated, condition);
            }
        }
    }
}
