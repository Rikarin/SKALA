using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1100</c> — a local that is initialised and immediately handed straight back.
/// </summary>
/// <remarks>
///     <c>var result = Compute(); return result;</c> is an extract-variable that was never undone: the
///     name adds a line and says nothing the expression did not, and a reader has to check that
///     nothing happens in between before they can read the two lines as one.
///     <para>
///         ⚠ <b>"In between" is the entire safety argument, and it is why the rule requires the use to
///         be the <em>very next statement</em>.</b> Inlining a temporary is normally unsafe precisely
///         because it moves the evaluation point: everything between the declaration and the use now
///         runs <em>before</em> the initializer instead of after it, so a side effect on either side
///         changes order. With nothing in between there is no order to change, and the initializer may
///         do anything it likes — which is what lets this ship as a fix that can be applied without
///         review rather than as the "requires a pure initializer" rule issue #82 proposed.
///     </para>
///     <para>
///         ⚠ <b>The declared type is the second hazard and <c>var</c> is not the only answer to it.</b>
///         <c>object M() { long v = 1; return v; }</c> boxes a <c>long</c>; <c>return 1;</c> boxes an
///         <c>int</c>. The declaration performed a conversion the <c>return</c> then did not have to,
///         and deleting it silently moves the conversion. So either the declaration is <c>var</c> — in
///         which case the local's type <em>is</em> the initializer's and there is no conversion to lose
///         — or the initializer's type is asked for and must equal the declared type exactly.
///     </para>
///     <para>
///         ⚠ <b>The single-use count is over the whole member, not the block.</b> A local function
///         declared further down is hoisted and may read the local from above its own declaration, so
///         a count that stopped at the enclosing block would delete a declaration something still
///         needs.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleUseTemporaryAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.SingleUseTemporary);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;

        // `using var`, `await using var`, `const`, `ref` and `scoped` all say something about the
        // local beyond its value, and none of them survives being deleted.
        if (declaration.UsingKeyword != default
            || declaration.AwaitKeyword != default
            || declaration.Modifiers.Count > 0
            || declaration.AttributeLists.Count > 0
            || declaration.Declaration.Type is RefTypeSyntax
            || declaration.Declaration.Variables.Count != 1) {
            return;
        }

        var declarator = declaration.Declaration.Variables[0];
        if (declarator.Initializer?.Value is not { } initializer) {
            return;
        }

        // ⚠ The adjacency guard. Anything at all between the declaration and the use is a statement
        // whose order relative to the initializer the rewrite would change.
        var name = declarator.Identifier.ValueText;
        var used = StatementRewrites.Next(declaration) switch {
            ReturnStatementSyntax { Expression: IdentifierNameSyntax identifier } => identifier,
            ThrowStatementSyntax { Expression: IdentifierNameSyntax identifier } => identifier,
            _ => null
        };

        if (used is null || !string.Equals(used.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
            return;
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;
        if (model.GetDeclaredSymbol(declarator, cancellation) is not ILocalSymbol local
            || local.IsRef
            || local.IsFixed
            || local.IsConst) {
            return;
        }

        if (!declaration.Declaration.Type.IsVar) {
            var type = model.GetTypeInfo(initializer, cancellation).Type;
            if (type is null || !SymbolEqualityComparer.Default.Equals(type, local.Type)) {
                return;
            }
        }

        if (ReadCount(model, local, RewriteGuards.ScopeRoot(declaration), name, cancellation) != 1) {
            return;
        }

        var tree = declaration.SyntaxTree;
        var text = tree.GetText();

        // The declaration's own line disappears and the identifier is replaced by the initializer.
        // Everything else in that region — the leading trivia above the declaration included — is
        // deleted, so a comment there withdraws the finding rather than being dropped.
        var statement = used.Parent!;
        if (StatementRewrites.DeletesAuthoredText(
                tree,
                TextSpan.FromBounds(declaration.FullSpan.Start, statement.Span.End),
                initializer.Span,
                used.Span
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(tree, declarator.Identifier.Span),
                FixEdits.Pack(
                    (RewriteGuards.LineSpanOf(declaration), string.Empty),
                    (used.Span, text.ToString(initializer.Span))
                ),
                "`" + name + "` is a name for an expression that is used once, on the next line"
            )
        );
    }

    /// <summary>How many times the local is named as a value anywhere in the member.</summary>
    /// <remarks>
    ///     ⚠ Filtered on the identifier's text before the symbol is asked for. Binding every
    ///     <c>IdentifierNameSyntax</c> in a member for every candidate declaration is the expensive
    ///     spelling of the same question, and the text filter is exact rather than heuristic: a
    ///     reference to this local is spelled with this local's name.
    /// </remarks>
    static int ReadCount(
        SemanticModel model,
        ILocalSymbol local,
        SyntaxNode root,
        string name,
        System.Threading.CancellationToken cancellation
    ) {
        var count = 0;
        foreach (var node in root.DescendantNodes()) {
            cancellation.ThrowIfCancellationRequested();
            if (node is not IdentifierNameSyntax identifier
                || !string.Equals(identifier.Identifier.ValueText, name, System.StringComparison.Ordinal)) {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellation).Symbol, local)) {
                count++;
                if (count > 1) {
                    return count;
                }
            }
        }

        return count;
    }
}
