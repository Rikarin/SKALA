using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Rikarin.Skala.Rules.Modernization;

/// <summary>
///     <c>SK1070</c> — <c>var a = t.Item1; var b = t.Item2;</c> is <c>var (a, b) = t;</c>.
/// </summary>
/// <remarks>
///     <para>
///         ⚠ <b>The whole risk is that deconstruction evaluates the receiver once and the longhand form
///         evaluates it once per element.</b> A property whose getter counts calls, a field behind a
///         lazy initializer, an indexer — each of them makes "read it twice" and "read it once" two
///         different programs, and none of that is visible at the assignment. Rather than reason about
///         which receivers are safe, the rule requires the receiver to be a single identifier naming a
///         <em>local or a parameter</em>. Reading one of those N times and reading it once are the same
///         program by construction, so there is nothing left to prove.
///     </para>
///     <para>
///         ⚠ <b>A partial deconstruction is not this rule.</b> <c>var a = t.Item1;</c> alone is a
///         variable with a name; it is not a deconstruction with three quarters missing. Every element
///         has to be read, exactly once, in positional order, or the rewrite invents variables the
///         author did not ask for.
///     </para>
///     <para>
///         ⚠ <b><c>var</c> on every declaration, never a written type.</b> <c>var (a, b) = t;</c> types
///         each variable as its element's type. A written type on one of the longhand declarations says
///         the author wanted a different one — a base class, an interface, a widened numeric — and
///         reproducing that needs the typed deconstruction form this fix does not emit.
///     </para>
///     <para>
///         ⚠ Real tuple types only. <c>System.Tuple&lt;T1, T2&gt;</c> also has <c>Item1</c> and
///         <c>Item2</c>, and it deconstructs only through <c>System.TupleExtensions</c> — so the same
///         rewrite there depends on a <c>using</c> the file may not have, and would stop compiling in
///         a file that does not.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TupleDeconstructionAnalyzer : DiagnosticAnalyzer {
    static readonly RuleInfo Rule = RuleCatalog.Get(RuleIds.TupleDeconstruction);
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.TupleDeconstruction);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start => {
                if (SkalaRule.MeetsLanguageVersion(start.Compilation, Rule.LanguageVersion)) {
                    start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
                }
            }
        );
    }

    static void Analyze(SyntaxNodeAnalysisContext context) {
        var first = (LocalDeclarationStatementSyntax)context.Node;

        // ⚠ Reported at the *first* element read only. A three-element run reported three times
        // would carry three fixes that each delete the lines the others rewrite.
        if (Reads(first) is not { Index: 0 } opening || Siblings(first) is not { } siblings) {
            return;
        }

        var start = -1;
        for (var i = 0; i < siblings.Count; i++) {
            if (siblings[i] == first) {
                start = i;
                break;
            }
        }

        var model = context.SemanticModel;
        var cancellation = context.CancellationToken;

        // ⚠ The receiver has to be a local or a parameter. That is what makes N reads and one read
        // the same program; every other receiver kind is a member access whose evaluation count the
        // rewrite would change.
        var receiver = model.GetSymbolInfo(opening.Receiver, cancellation).Symbol;
        if (receiver is not (ILocalSymbol or IParameterSymbol)) {
            return;
        }

        if (model.GetTypeInfo(opening.Receiver, cancellation).Type is not INamedTypeSymbol {
                IsTupleType: true
            } tuple) {
            return;
        }

        var arity = tuple.TupleElements.Length;
        if (start < 0 || arity < 2 || start + arity > siblings.Count) {
            return;
        }

        var names = new List<string>(arity);
        for (var i = 0; i < arity; i++) {
            cancellation.ThrowIfCancellationRequested();
            if (siblings[start + i] is not LocalDeclarationStatementSyntax statement
                || Reads(statement) is not { } read
                || read.Index != i
                || !RewriteGuards.Same(read.Receiver, opening.Receiver)
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(read.Receiver, cancellation).Symbol,
                    receiver
                )) {
                return;
            }

            names.Add(read.Name);
        }

        var last = siblings[start + arity - 1];
        if (RewriteGuards.ContainsCommentOrDirective(
                first.SyntaxTree,
                TextSpan.FromBounds(first.SpanStart, last.FullSpan.End)
            )) {
            return;
        }

        var replacement = new StringBuilder("var (");
        for (var i = 0; i < names.Count; i++) {
            if (i > 0) {
                replacement.Append(", ");
            }

            replacement.Append(names[i]);
        }

        replacement.Append(") = ").Append(opening.Receiver.ToString()).Append(';');

        var edits = new List<(TextSpan Span, string Text)>(arity) { (first.Span, replacement.ToString()) };
        for (var i = 1; i < arity; i++) {
            edits.Add((RewriteGuards.LineSpanOf(siblings[start + i]), string.Empty));
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                Location.Create(first.SyntaxTree, TextSpan.FromBounds(first.SpanStart, last.Span.End)),
                FixEdits.Pack(edits.ToArray()),
                "The tuple is read element by element: `" + RewriteGuards.Trim(replacement.ToString()) + "`"
            )
        );
    }

    /// <summary>
    ///     The statement's <c>var x = receiver.ItemK;</c> shape, or null when it is not one.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>ItemK</c> is matched by name and the receiver's type is then required to be a tuple.
    ///     The two together are exact — a tuple type has no other member spelled that way — while
    ///     matching the field symbol would additionally have to cope with a named element, where the
    ///     friendly name and <c>ItemK</c> are two <see cref="IFieldSymbol" />s for one storage slot.
    /// </remarks>
    static (ExpressionSyntax Receiver, int Index, string Name)? Reads(LocalDeclarationStatementSyntax statement) {
        if (statement.UsingKeyword.RawKind != (int)SyntaxKind.None
            || statement.AwaitKeyword.RawKind != (int)SyntaxKind.None
            || statement.Modifiers.Count > 0
            || statement.AttributeLists.Count > 0
            || statement.Declaration.Variables.Count != 1
            || !statement.Declaration.Type.IsVar) {
            return null;
        }

        var declarator = statement.Declaration.Variables[0];
        if (declarator.Initializer?.Value is not MemberAccessExpressionSyntax {
                RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                Expression: IdentifierNameSyntax receiver,
                Name: IdentifierNameSyntax member
            }) {
            return null;
        }

        var text = member.Identifier.ValueText;
        if (text.Length <= 4
            || !text.StartsWith("Item", System.StringComparison.Ordinal)
            || !int.TryParse(
                text.Substring(4),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var position
            )
            || position < 1) {
            return null;
        }

        return (receiver, position - 1, declarator.Identifier.ValueText);
    }

    /// <summary>
    ///     The statements sharing this one's block or switch section, in source order.
    /// </summary>
    /// <remarks>
    ///     ⚠ Every statement is returned, not only the declarations. The run has to be
    ///     <em>consecutive</em>, and filtering the others out would let an intervening statement — a
    ///     call that reassigns the receiver between two element reads — vanish from the run it breaks.
    /// </remarks>
    static IReadOnlyList<StatementSyntax>? Siblings(StatementSyntax statement) =>
        statement.Parent switch {
            BlockSyntax block => block.Statements,
            SwitchSectionSyntax section => section.Statements,
            _ => null
        };
}
