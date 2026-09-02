using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1092</c> — <c>var p = new Tuple&lt;int, string&gt;(1, "a");</c> is <c>var p = (1, "a");</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠
///         <b>
///             <c>System.Tuple</c> is a class and <c>System.ValueTuple</c> is a struct, so the rewrite
///             changes nullability, equality and allocation at once — and <c>Item1</c>-style access
///             works on both, which means the shape alone proves nothing.
///         </b> The rule therefore never
///         reports a construction that could escape. It reports a <em>local declaration</em>, and only
///         when every reference to that local in the enclosing member is a <c>t.ItemN</c> read: a
///         local that is returned, passed, assigned, reassigned, compared with <c>==</c> (identity on
///         <c>Tuple</c>, structural on <c>ValueTuple</c>), tested with <c>is</c> or cast is left alone,
///         because each of those is a place the difference is observable.
///     </para>
///     <para>
///         ⚠ <b>Arity 2 to 7.</b> <c>Tuple&lt;T&gt;</c> has no literal form — <c>(x)</c> is a
///         parenthesized expression — and an eighth element is <c>TRest</c>, whose nesting the fix
///         does not reproduce.
///     </para>
///     <para>
///         ⚠ <b>The written type arguments are what the fix reuses</b>, never a display string derived
///         from the symbol, so a type spelled through a <c>using</c> alias or reachable only from this
///         file keeps compiling.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TupleLiteralAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.TupleLiteral);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TupleLiteral);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                // ⚠ A target framework without `System.ValueTuple` would take a fix that does not
                // compile. The language floor alone does not answer that question.
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)
                    && start.Compilation.GetTypeByMetadataName("System.ValueTuple`2") is not null) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var statement = (LocalDeclarationStatementSyntax)context.Node;
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.AttributeLists.Count > 0
            || statement.Declaration.Variables.Count != 1) {
            return;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer?.Value is not { } value) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        if (Arguments(value) is not { } arguments
            || arguments.Arguments.Count is < 2 or > 7
            || arguments.Arguments.Any(static argument => argument.NameColon is not null
                || !argument.RefKindKeyword.IsKind(SyntaxKind.None)
            )
            || model.GetTypeInfo(value, cancellation).Type is not INamedTypeSymbol created
            || created.IsTupleType
            || created.Name != "Tuple"
            || created.Arity != arguments.Arguments.Count
            || created.ContainingNamespace?.ToDisplayString() != "System") {
            return;
        }

        // ⚠ The declaration keeps `var` when it was written `var`. Rewriting the declared type is
        // `SK0202`'s subject, and this rule may not take a position on it.
        var declared = statement.Declaration.Type;
        var edits = new List<(TextSpan Span, string Text)>();
        if (!declared.IsVar) {
            if (AsTupleName(declared) is not { } written
                || written.TypeArgumentList.Arguments.Count != arguments.Arguments.Count) {
                return;
            }

            edits.Add((declared.Span, Join(written.TypeArgumentList.Arguments.Select(static a => a.ToString()))));
        }

        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol local
            || !OnlyReadsElements(model, statement, local, cancellation)) {
            return;
        }

        // ⚠ The two spans the fix rewrites, not the statement. Asking the statement reads its
        // leading trivia, so a comment on the line above declined a declaration whose own text the
        // fix never touches.
        if (RewriteGuards.ContainsCommentOrDirective(
                statement.SyntaxTree,
                TextSpan.FromBounds(statement.Declaration.Type.SpanStart, value.Span.End)
            )) {
            return;
        }

        var literal = Join(arguments.Arguments.Select(static argument => argument.Expression.ToString()));
        edits.Add((value.Span, literal));

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                value.GetLocation(),
                FixEdits.Pack(edits.ToArray()),
                "The `Tuple` is only read element by element: `" + RewriteGuards.Trim(literal) + "`"
            )
        );
    }

    /// <summary>
    ///     The <c>Tuple&lt;…&gt;</c> at the end of a written type name, or null when it is not one.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>System.Tuple&lt;int, string&gt;</c> is a <see cref="QualifiedNameSyntax" /> and
    ///     <c>Tuple&lt;int, string&gt;</c> is a <see cref="GenericNameSyntax" />. Matching only the
    ///     second silently declined every fully-qualified spelling — measured, on the rule's own first
    ///     positive fixture.
    /// </remarks>
    static GenericNameSyntax? AsTupleName(TypeSyntax type) =>
        type switch {
            GenericNameSyntax { Identifier.ValueText: "Tuple" } generic => generic,
            QualifiedNameSyntax qualified => AsTupleName(qualified.Right),
            AliasQualifiedNameSyntax aliased => AsTupleName(aliased.Name),
            _ => null
        };

    /// <summary>The argument list of a <c>new Tuple&lt;…&gt;(…)</c> or a <c>Tuple.Create(…)</c>.</summary>
    static ArgumentListSyntax? Arguments(ExpressionSyntax value) =>
        value switch {
            ObjectCreationExpressionSyntax creation when AsTupleName(creation.Type) is not null =>
                creation.ArgumentList,
            InvocationExpressionSyntax {
                Expression: MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Name.Identifier.ValueText: "Create"
                }
            } invocation => invocation.ArgumentList,
            _ => null
        };

    /// <summary>
    ///     Whether every reference to the local is a <c>local.ItemN</c> read.
    /// </summary>
    /// <remarks>
    ///     ⚠ The census is over the whole member, not the block, because a lambda written further
    ///     down closes over the same local and observes the same difference. Anything that is not an
    ///     element read — an argument, a return, a comparison, a reassignment, a cast — withdraws the
    ///     finding, because that is where a class and a struct stop being interchangeable.
    /// </remarks>
    static bool OnlyReadsElements(
        SemanticModel model,
        SyntaxNode statement,
        ILocalSymbol local,
        System.Threading.CancellationToken cancellation
    ) {
        var found = false;
        foreach (var node in RewriteGuards.ScopeRoot(statement).DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax name
                || name.Identifier.ValueText != local.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellation).Symbol, local)) {
                continue;
            }

            if (name.Parent is not MemberAccessExpressionSyntax {
                    RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                    Name: IdentifierNameSyntax member
                } access
                || access.Expression != name
                || !IsElement(member.Identifier.ValueText)) {
                return false;
            }

            found = true;
        }

        return found;
    }

    static bool IsElement(string text) =>
        text.Length > 4
        && text.StartsWith("Item", System.StringComparison.Ordinal)
        && int.TryParse(
            text.Substring(4),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var position
        )
        && position >= 1;

    static string Join(IEnumerable<string> parts) {
        var builder = new StringBuilder("(");
        var first = true;
        foreach (var part in parts) {
            if (!first) {
                builder.Append(", ");
            }

            builder.Append(part);
            first = false;
        }

        return builder.Append(')').ToString();
    }
}
