using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rikarin.Skala.Rules.Maintainability;

/// <summary>
///     <c>SK7072</c> — a <c>#pragma warning disable</c> whose region holds no code at all.
/// </summary>
/// <remarks>
///     ⚠
///     <b>
///         This is the decidable half of "the suppression has nothing to suppress", and the other half
///         is not decidable here at all.
///     </b> Deciding that a disabled warning "no longer fires" means
///     knowing what the compilation would report <em>without</em> the pragma, and an analyzer cannot
///     ask that question: pragma filtering is applied to compiler diagnostics before
///     <c>Compilation.GetDiagnostics</c> returns, an analyzer cannot enumerate the other analyzers in
///     the run, and re-analysing from inside an analyzer is re-entrant. Only the host — which already
///     passes <c>reportSuppressedDiagnostics: true</c> — is in a position to answer it, and that is a
///     different feature from this one.
///     <para>
///         What is decidable is the degenerate case, which is also the one an editing accident actually
///         produces: the code the pragma bracketed was deleted and the bracket stayed. No code inside
///         means no diagnostic inside, for every analyzer that will ever run, without knowing any of
///         them. The fix deletes the directives and nothing else.
///     </para>
///     <para>
///         ⚠ A region holding only other <c>#pragma warning</c> directives counts as empty, and that is
///         not a convenience. Nested empty brackets are one edit, not a sequence of them: reporting
///         only the innermost pair would make <c>skala fix</c> converge over several passes, which is
///         exactly the loop <c>EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic</c> exists to catch.
///         Every other trivia — a comment, a <c>#region</c>, inactive <c>#if</c> text — counts as
///         content and the rule stays quiet, because under another <c>-d</c> the inactive text is code.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptySuppressionRegionAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.EmptySuppressionRegion);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(Analyze);
    }

    static void Analyze(SyntaxTreeAnalysisContext context) {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var directives = root.DescendantNodes(descendIntoTrivia: true)
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .ToList();
        if (directives.Count == 0) {
            return;
        }

        var source = context.Tree.GetText(context.CancellationToken);
        var content = ContentPositions(root);

        for (var i = 0; i < directives.Count; i++) {
            var disable = directives[i];
            if (!disable.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)) {
                continue;
            }

            var codes = Codes(disable);
            var restore = Matching(directives, i, codes);
            var end = restore?.FullSpan.Start ?? root.FullSpan.End;
            var region = TextSpan.FromBounds(Math.Min(disable.FullSpan.End, end), end);
            if (content.Any(region.Contains)) {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptor,
                    disable.GetLocation(),
                    Edits(source, disable, restore),
                    restore is null
                        ? "the suppression covers nothing: no code follows it"
                        : "the suppression covers nothing: it is closed again with no code in between"
                )
            );
        }
    }

    /// <summary>
    ///     Every position in the file that is code as far as this rule is concerned.
    /// </summary>
    /// <remarks>
    ///     Zero-width tokens are skipped — the end-of-file token is one, and treating it as content
    ///     would make a trailing suppression look occupied by nothing.
    /// </remarks>
    static List<int> ContentPositions(SyntaxNode root) {
        var result = new List<int>();
        foreach (var token in root.DescendantTokens()) {
            if (token.Span.Length > 0) {
                result.Add(token.SpanStart);
            }
        }

        foreach (var trivia in root.DescendantTrivia()) {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)
                && !trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia)) {
                result.Add(trivia.SpanStart);
            }
        }

        result.Sort();
        return result;
    }

    /// <summary>The first later <c>restore</c> that lifts this <c>disable</c>.</summary>
    /// <remarks>
    ///     ⚠ A <c>restore</c> naming other ids does not close this one, and a bare <c>restore</c> closes
    ///     everything. Getting that backwards would end the region at the wrong directive and report a
    ///     bracket around real code.
    /// </remarks>
    static PragmaWarningDirectiveTriviaSyntax? Matching(
        List<PragmaWarningDirectiveTriviaSyntax> directives,
        int index,
        HashSet<string> codes
    ) {
        for (var j = index + 1; j < directives.Count; j++) {
            var candidate = directives[j];
            if (!candidate.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword)) {
                continue;
            }

            var restored = Codes(candidate);
            if (restored.Count == 0 || (codes.Count > 0 && codes.IsSubsetOf(restored))) {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    ///     The ids a directive names, normalised so that <c>618</c> and <c>CS0618</c> are one id.
    /// </summary>
    static HashSet<string> Codes(PragmaWarningDirectiveTriviaSyntax directive) {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in directive.ErrorCodes) {
            var text = code.ToString().Trim();
            if (text.Length == 0) {
                continue;
            }

            result.Add(
                int.TryParse(
                    text,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var number
                )
                    ? "CS" + number.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)
                    : text
            );
        }

        return result;
    }

    /// <summary>Delete the directive lines, whole, including any trailing justification comment.</summary>
    static ImmutableDictionary<string, string?> Edits(
        SourceText source,
        PragmaWarningDirectiveTriviaSyntax disable,
        PragmaWarningDirectiveTriviaSyntax? restore
    ) =>
        restore is null
            ? FixEdits.Pack((Line(source, disable), string.Empty))
            : FixEdits.Pack((Line(source, disable), string.Empty), (Line(source, restore), string.Empty));

    static TextSpan Line(SourceText source, PragmaWarningDirectiveTriviaSyntax directive) {
        var line = source.Lines.GetLineFromPosition(directive.SpanStart);
        return TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak);
    }
}
