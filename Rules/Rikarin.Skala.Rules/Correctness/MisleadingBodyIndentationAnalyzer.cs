using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Rules.Metadata;
using Rikarin.Skala.Rules.Modernization;
using System;
using System.Collections.Immutable;

namespace Rikarin.Skala.Rules.Correctness;

/// <summary>
///     <c>SK2170</c> — the statement below an unbraced body is indented as though it were inside it.
/// </summary>
/// <remarks>
///     <code>
///     if (stale)
///         Reload();
///         Publish();
///     </code>
///     <c>Publish()</c> runs every time. The grammar gives the <c>if</c> exactly one statement and the
///     picture gives it two, and the reader believes the picture.
///     <para>
///         ⚠ <b>The other half of this concept belongs to the compiler, and that was measured.</b> An
///         empty statement standing in for a body — <c>if (x); { … }</c> — is <c>CS0642</c>, on by
///         default, and it reaches further than doc 17 supposed: it covers <c>if</c>, <c>else</c>,
///         <c>lock</c>, <c>do</c>, <c>using</c> and <c>fixed</c> outright, and covers <c>while</c>,
///         <c>for</c> and <c>foreach</c> exactly when a block follows the <c>;</c> — which is precisely
///         the misleading shape. A probe on SDK 10.0.400 compiled all nine and read the warnings off
///         the build. There was nothing left to add, so that half is not here.
///     </para>
///     <para>
///         ⚠ <b>Indentation is not structure anywhere in C#, so nothing but a formatter looks at
///         it.</b> This rule reads the leading whitespace of three lines and compares them as strings —
///         a question no semantic model can be asked, and the second place in the catalogue where
///         trivia rather than structure decides a correctness finding. <c>SK2063</c> is the first.
///     </para>
///     <para>
///         ⚠ <b>Prefix comparison, never a column count.</b> One tab and one space are the same column
///         and different indentation; comparing the whitespace as strings means a tab-indented file
///         compares tab prefixes, and a line that mixes the two fails the prefix test and is declined
///         rather than guessed at.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MisleadingBodyIndentationAnalyzer : DiagnosticAnalyzer {
    static readonly DiagnosticDescriptor Descriptor = SkalaRule.Descriptor(RuleIds.MisleadingBodyIndentation);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchSection, SyntaxKind.SwitchSection);
    }

    static void AnalyzeBlock(SyntaxNodeAnalysisContext context) =>
        AnalyzeList(context, ((BlockSyntax)context.Node).Statements);

    static void AnalyzeSwitchSection(SyntaxNodeAnalysisContext context) =>
        AnalyzeList(context, ((SwitchSectionSyntax)context.Node).Statements);

    static void AnalyzeList(SyntaxNodeAnalysisContext context, SyntaxList<StatementSyntax> statements) {
        if (statements.Count < 2) {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        for (var i = 0; i + 1 < statements.Count; i++) {
            Compare(context, text, statements[i], statements[i + 1]);
        }
    }

    static void Compare(SyntaxNodeAnalysisContext context, SourceText text, StatementSyntax header, StatementSyntax next) {
        if (TrailingBody(header) is not { } body || body is BlockSyntax) {
            return;
        }

        // ⚠ An empty statement here is `CS0642`'s, not this rule's — see the type remarks. The
        // grammar cannot indent-mislead with one either: `;` carries no statement to read as a body.
        if (body is EmptyStatementSyntax) {
            return;
        }

        var headerLine = text.Lines.GetLineFromPosition(header.SpanStart);
        var bodyLine = text.Lines.GetLineFromPosition(body.SpanStart);
        var bodyEndLine = text.Lines.GetLineFromPosition(body.Span.End);
        var nextLine = text.Lines.GetLineFromPosition(next.SpanStart);

        // Three lines, each below the last, and each statement first on its own. A body sharing the
        // header's line has no layout to mislead anyone, and a statement that does not begin its
        // line is being indented by something other than itself.
        if (bodyLine.LineNumber <= headerLine.LineNumber
            || nextLine.LineNumber <= bodyEndLine.LineNumber
            || Indent(text, headerLine, header.SpanStart) is not { } headerIndent
            || Indent(text, bodyLine, body.SpanStart) is not { } bodyIndent
            || Indent(text, nextLine, next.SpanStart) is not { } nextIndent) {
            return;
        }

        // The body is indented under the header, and the next statement is aligned with the body —
        // so the picture puts both in a column, inside a block that does not exist.
        //
        // ⚠ Alignment, not "at least as deep", and the corpus is what settled it. The looser test
        // reports four times on `unformatted/scramble/`, a slice whose whitespace has been
        // randomised on purpose: there the following statement lands 2, 4 or 6 columns *past* the
        // body, which reads as mangled or as a continuation and not as a sibling. All four are
        // declined by asking for the column a reader would actually see.
        if (bodyIndent.Length <= headerIndent.Length
            || !bodyIndent.StartsWith(headerIndent, StringComparison.Ordinal)
            || !string.Equals(nextIndent, bodyIndent, StringComparison.Ordinal)) {
            return;
        }

        // ⚠ Under an `#if` the two statements are not necessarily both in the program, and the
        // indentation of a conditionally compiled region is a convention rather than a claim.
        if (RewriteGuards.ContainsCommentOrDirective(
                header.SyntaxTree,
                TextSpan.FromBounds(body.Span.End, next.SpanStart)
            )) {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptor,
                next.GetFirstToken().GetLocation(),
                "the `"
                + Keyword(body.Parent)
                + "` above has one unbraced statement as its body, so this one runs unconditionally; "
                + "the indentation says otherwise"
            )
        );
    }

    /// <summary>
    ///     The statement a reader would take the following line to belong to: the innermost embedded
    ///     statement in trailing position.
    /// </summary>
    /// <remarks>
    ///     ⚠ An <c>else</c> is followed down, because it is the <c>else</c>'s body that the next line
    ///     sits under: in <c>if (a) A(); else B(); C();</c> laid out over five lines, <c>C()</c> is
    ///     compared against <c>B()</c>.
    ///     <para>
    ///         ⚠ <c>do</c> is absent deliberately. Its body is not trailing — the <c>while</c> follows
    ///         it — so nothing below the statement can be misread as belonging to it.
    ///     </para>
    /// </remarks>
    static StatementSyntax? TrailingBody(StatementSyntax statement) {
        StatementSyntax? current = null;
        while (true) {
            var inner = statement switch {
                IfStatementSyntax @if => @if.Else?.Statement ?? @if.Statement,
                WhileStatementSyntax @while => @while.Statement,
                ForStatementSyntax @for => @for.Statement,
                ForEachStatementSyntax @foreach => @foreach.Statement,
                ForEachVariableStatementSyntax deconstructing => deconstructing.Statement,
                LockStatementSyntax @lock => @lock.Statement,
                UsingStatementSyntax @using => @using.Statement,
                FixedStatementSyntax @fixed => @fixed.Statement,
                _ => null
            };

            if (inner is null) {
                return current;
            }

            current = inner;
            statement = inner;
        }
    }

    /// <summary>
    ///     The line's leading whitespace, or <c>null</c> when the statement is not what starts the line.
    /// </summary>
    static string? Indent(SourceText text, TextLine line, int position) {
        var start = line.Start;
        var i = start;
        while (i < position && (text[i] == ' ' || text[i] == '\t')) {
            i++;
        }

        return i == position ? text.ToString(TextSpan.FromBounds(start, i)) : null;
    }

    static string Keyword(SyntaxNode? owner) =>
        owner switch {
            ElseClauseSyntax => "else",
            IfStatementSyntax => "if",
            WhileStatementSyntax => "while",
            ForStatementSyntax => "for",
            ForEachStatementSyntax or ForEachVariableStatementSyntax => "foreach",
            LockStatementSyntax => "lock",
            UsingStatementSyntax => "using",
            FixedStatementSyntax => "fixed",
            _ => "statement"
        };
}
