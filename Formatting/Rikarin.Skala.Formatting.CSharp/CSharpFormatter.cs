using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What formatting one file produced.</summary>
/// <param name="ReflowedComments">
///     ⚠ How many documentation comments the sub-formatter rewrote, and it is on the result rather
///     than inside the pipeline because the property cannot be checked from outside without it.
///     A re-wrapped <c>///</c> comment changes documentation trivia, which
///     <see cref="TokenEquivalence" /> only forgives when told a reflow happened; a caller that
///     compares the two texts itself and cannot ask will report every reflowed file as a violation.
///     The fuzzer did exactly that the day the sub-formatter became the default.
/// </param>
public sealed record FormatResult(
    string Path,
    SourceText Original,
    ImmutableArray<TextEdit> Edits,
    string Formatted,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    FormatOutcome Outcome,
    int ReflowedComments = 0) {
    public bool Changed => !Edits.IsEmpty;

    public int ChangedLines {
        get {
            var lines = 0;
            foreach (var edit in Edits) {
                lines += 1
                    + CSharpDocumentBuilder.CountNewLines(
                        Original.ToString(
                            Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(edit.Span.Start, edit.Span.End)
                        )
                    );
            }

            return lines;
        }
    }
}

/// <summary>How far the pipeline got.</summary>
public enum FormatOutcome {
    Formatted,

    /// <summary>The file does not parse (ADR-003). Reported, left byte-identical.</summary>
    NotParseable,

    /// <summary>Generated code, skipped by policy.</summary>
    Generated,

    /// <summary>⚠ The output's token stream differed from the input's. Nothing was written.</summary>
    VerificationFailed
}

/// <summary>
///     The pipeline of docs/plan/04 § "The pipeline", minus the arrangement and fitting stages that
///     milestones 2–4 fill in.
/// </summary>
public static class CSharpFormatter {
    public static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Preview).WithDocumentationMode(DocumentationMode.Parse);

    static readonly ConcurrentDictionary<string, CSharpParseOptions> SymbolisedOptions = new(StringComparer.Ordinal);

    /// <summary>
    ///     The parse options for a file, given the preprocessor symbols that are defined for it.
    /// </summary>
    /// <remarks>
    ///     ⚠ SK-DIV-0004. With no symbols Roslyn hands back every <c>#if DEBUG</c> body as
    ///     <see cref="SyntaxKind.DisabledTextTrivia" /> — an unstructured string the formatter is not
    ///     allowed to touch — while the oracle runs against a project where <c>DEBUG</c> is defined and
    ///     formats it. Nothing else in the pipeline needs to change: which branch is disabled text is
    ///     entirely a parse-time decision, so supplying the symbols is the whole fix.
    ///     <para>
    ///         Memoised because the symbol set is per-compilation and the file count per compilation is in
    ///         the thousands; <see cref="CSharpParseOptions.WithPreprocessorSymbols(IEnumerable{string})" />
    ///         allocates a new options object and a new symbol map every call.
    ///     </para>
    /// </remarks>
    public static CSharpParseOptions ParseOptionsFor(IReadOnlyList<string>? symbols) {
        if (symbols is null || symbols.Count == 0) {
            return ParseOptions;
        }

        // Ordinal-sorted and de-duplicated, so that two orderings of one set share a cache entry
        // and — more importantly — produce the same parse. Roslyn's symbol map is a set; the key
        // has to be one too or the memo is keyed on something the answer does not depend on.
        var normalised = new SortedSet<string>(symbols, StringComparer.Ordinal);
        var key = string.Join(";", normalised);
        return SymbolisedOptions.GetOrAdd(key, _ => ParseOptions.WithPreprocessorSymbols(normalised));
    }

    /// <summary>Formats text that has already been read, with options that have already been resolved.</summary>
    public static FormatResult Format(
        string path,
        SourceText text,
        in FormattingOptions options,
        string? crashRoot = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        bool xmlDoc = true
    ) {
        var phaseOne = new PhaseOneOptions(options);
        return Format(path, text, phaseOne, crashRoot, preprocessorSymbols, xmlDoc);
    }

    /// <summary>
    ///     ⚠ <paramref name="xmlDoc" /> defaults to <c>true</c>: documentation comments are formatted.
    /// </summary>
    /// <remarks>
    ///     ⚠ On by default, and the default changed. It was off from milestone 9 because
    ///     <c>jb cleanupcode</c> <em>under the committed profiles</em> does not format documentation
    ///     comments (SK-DIV-0006 — it does under one that enables <c>CSharpFormatDocComments</c>) and the oracle is
    ///     the definition of correct under ADR-011 — so re-wrapping them by default read as a 3.59-point
    ///     fidelity regression. The premise was wrong in one specific way —
    ///     Rider formats them and
    ///     the pinned profile does not — so the two disagree, and matching the oracle here
    ///     means diverging from Rider on every documentation comment in every repository. Skala follows
    ///     Rider. The consequence is that this is the one area of the formatter with no differential
    ///     safety net at all, which is why <see cref="XmlDocFormatter" /> carries a round-trip check on
    ///     every comment of every run rather than a fixture.
    ///     <para>
    ///         ⚠ The escape hatch is <c>skala format --no-xmldoc</c> and not
    ///         <c>resharper_xmldoc_wrap_lines = false</c>. That key means "do not wrap long lines" — with it
    ///         false the sub-formatter still re-indents, still collapses blank lines between tags, still
    ///         inserts the marker space — so making it the kill switch would attach a meaning to a
    ///         ReSharper key that ReSharper does not give it, which is the mistake this change is undoing.
    ///     </para>
    /// </remarks>
    public static FormatResult Format(
        string path,
        SourceText text,
        in PhaseOneOptions options,
        string? crashRoot = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        bool xmlDoc = true
    ) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        if (GeneratedCode.IsGenerated(path, text)) {
            return new FormatResult(
                path,
                text,
                [],
                text.ToString(),
                diagnostics.ToImmutable(),
                FormatOutcome.Generated
            );
        }

        var parseOptions = ParseOptionsFor(preprocessorSymbols);
        var tree = CSharpSyntaxTree.ParseText(text, parseOptions, path);
        foreach (var diagnostic in tree.GetDiagnostics()) {
            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            // ⚠ ADR-003: a file that does not parse is reported and left byte-identical. This is the
            // single most important safety property in the tool.
            var position = diagnostic.Location.GetLineSpan().StartLinePosition;
            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.NotParseable,
                    SkalaSeverity.Warning,
                    $"not formatted, the file does not parse: {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}",
                    path,
                    position.Line + 1
                )
            );

            return new FormatResult(
                path,
                text,
                [],
                text.ToString(),
                diagnostics.ToImmutable(),
                FormatOutcome.NotParseable
            );
        }

        // ⚠ `disable_formatter = true` and the pass is over. Placed *after* the two gates above and
        // not before them, so that a file that does not parse is still reported under ADR-003 and a
        // generated file still reports as generated: switching the formatter off is a statement about
        // whitespace, not a reason to stop looking at the file. Everything below this line — the
        // document build, the layout, the xmldoc sub-formatter, int-align, `insert_final_newline` —
        // is skipped, because the oracle's answer to this key is the input byte for byte and not a
        // gentler formatting (SK-DIV-0060).
        if (options.DisableFormatter) {
            return new FormatResult(
                path,
                text,
                [],
                text.ToString(),
                diagnostics.ToImmutable(),
                FormatOutcome.Formatted
            );
        }

        var root = tree.GetRoot();
        XmlDocComments.Report(path, text, root, diagnostics);

        var built = CSharpDocumentBuilder.Build(path, text, root, options);
        diagnostics.AddRange(built.Diagnostics);

        var indentUnit = options.UseTabs ? "\t" : new string(' ', options.IndentSize);
        var newLine = DefaultNewLine(text, options);
        var layout = LayoutWriter.Write(
            built.Document,
            options.MaxLineLength,
            indentUnit,
            newLine,
            options.ContinuousIndentMultiplier,
            // ⚠ `disable_indenter = true` and the writer needs the input, because "keep the leading
            // whitespace the author wrote" cannot be answered from the document alone. Materialised
            // only when the key is on, so the ordinary path does not pay for a whole-file string.
            options.DisableIndenter ? text.ToString() : null,
            options.TabFill
        );
        // ⚠ Two post-passes over the laid-out text, and the order between them is a decision.
        // The xmldoc sub-formatter goes first because it re-wraps comments against the *final* code
        // indentation, and column alignment goes second because it measures the widest of a run of
        // siblings and must see text nothing will move again. Reversing them would align against
        // columns the reflow then changes.
        var output = layout.Text;

        var reflowed = 0;
        if (xmlDoc) {
            var outcome = XmlDocFormatter.Rewrite(output, options.XmlDoc, parseOptions, newLine, options.Tags);
            layout = XmlDocFormatter.Reanchor(layout, outcome.Text, outcome.Replacements);
            output = outcome.Text;
            reflowed = outcome.Reflowed;
        }

        output = ApplyFileLevelRules(IntAlign.Apply(output, options, parseOptions), options, newLine);

        ReportLongLines(path, output, options, diagnostics);
        var edits = EditEmitter.Emit(text.ToString(), layout with { Text = output });
        var formatted = EditEmitter.Apply(text.ToString(), edits);

        var after = SourceText.From(formatted, text.Encoding ?? System.Text.Encoding.UTF8);
        if (ForcedVerificationFailure(path) is { } forced) {
            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.TokenStreamChanged,
                    SkalaSeverity.Error,
                    "not written, the formatted output has a different token stream " + forced,
                    path,
                    0,
                    "Forced by SKALA_FORCE_SK9099. This is the harness, not a Skala bug."
                )
            );

            return new FormatResult(
                path,
                text,
                [],
                text.ToString(),
                diagnostics.ToImmutable(),
                FormatOutcome.VerificationFailed
            );
        }

        if (TokenEquivalence.Compare(text, after, parseOptions, reflowed > 0, options.XmlDoc.SpaceAfterTripleSlash)
            is { } failure) {
            var artefact = CrashArtifacts.Write(crashRoot, path, text.ToString(), formatted, options);
            diagnostics.Add(
                new SkalaDiagnostic(
                    FormatDiagnosticIds.TokenStreamChanged,
                    SkalaSeverity.Error,
                    $"not written, the formatted output has a different token stream (at token {failure.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}: '{failure.Before}' became '{failure.After}')",
                    path,
                    0,
                    artefact is null
                        ? null
                        : $"A reproduction is in {artefact}. This is a Skala bug; the file was left untouched."
                )
            );

            return new FormatResult(
                path,
                text,
                [],
                text.ToString(),
                diagnostics.ToImmutable(),
                FormatOutcome.VerificationFailed
            );
        }

        return new FormatResult(
            path,
            text,
            [.. edits],
            formatted,
            diagnostics.ToImmutable(),
            FormatOutcome.Formatted,
            reflowed
        );
    }

    /// <summary>
    ///     Reports every line the formatter could not fit, at <c>hint</c>.
    /// </summary>
    /// <remarks>
    ///     ⚠ docs/plan/04 § "The fitting algorithm": "Unfittable lines are left long. […] It never
    ///     breaks a token, never breaks inside a string, and never emits a diagnostic for it by default
    ///     (SK0002 at hint for the audit)." A 200-character string literal and a deeply-qualified
    ///     generic type are not the formatter's to shorten, and a formatter that tried would be breaking
    ///     tokens.
    /// </remarks>
    static void ReportLongLines(
        string path,
        string output,
        in PhaseOneOptions options,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        var line = 1;
        var start = 0;
        for (var i = 0; i <= output.Length; i++) {
            if (i != output.Length && output[i] != '\n') {
                continue;
            }

            var end = i > start && output[i - 1] == '\r' ? i - 1 : i;
            if (TextWidth.Measure(output[start..end]) > options.MaxLineLength) {
                diagnostics.Add(
                    new SkalaDiagnostic(
                        FormatDiagnosticIds.LineTooLong,
                        SkalaSeverity.Hidden,
                        $"the line is {TextWidth.Measure(output[start..end]).ToString(System.Globalization.CultureInfo.InvariantCulture)} columns and nothing in it could break",
                        path,
                        line
                    )
                );
            }

            start = i + 1;
            line++;
        }
    }

    /// <summary>Reads a file, resolves its options from the .editorconfig chain, and formats it.</summary>
    public static FormatResult FormatFile(
        string path,
        IReadOnlyList<KeyValuePair<string, string>>? overrides = null,
        string? crashRoot = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        bool xmlDoc = true
    ) {
        var text = Read(path);
        // ⚠ Through ConfigurationCache: resolving 483 options from a re-parsed chain per file is
        // most of what `format --check` over a large tree spends its time on, and the answer is the
        // same for every file the same sections match (docs/plan/13 § "The fitting pass").
        var options = ConfigurationCache.Options(EditorConfigChain.For(path), overrides);
        return Format(path, text, options, crashRoot, preprocessorSymbols, xmlDoc);
    }

    public static SourceText Read(string path) {
        using var stream = File.OpenRead(path);
        return SourceText.From(stream, canBeEmbedded: false);
    }

    /// <summary>
    ///     Final newline and the trailing-whitespace sweep, applied last and as ordinary edits.
    /// </summary>
    /// <remarks>
    ///     ⚠ <c>resharper_csharp_insert_final_newline = true</c> wins over
    ///     <c>
    /// [*] insert_final_newline
    ///  = false
    ///     </c> by language specificity (docs/plan/03, hazard 3). The BOM is preserved exactly:
    ///     it lives in <see cref="SourceText.Encoding" /> and never in the text, so nothing here can add
    ///     or remove one.
    /// </remarks>
    static string ApplyFileLevelRules(string output, in PhaseOneOptions options, string newLine) {
        var trimmed = output.TrimEnd(' ', '\t', '\r', '\n');
        if (trimmed.Length == 0) {
            return options.InsertFinalNewline && output.Length > 0 ? newLine : output;
        }

        return options.InsertFinalNewline ? trimmed + FinalNewLine(trimmed, options, newLine) : trimmed;
    }

    /// <summary>
    ///     The ending for the newline <c>insert_final_newline</c> adds: the one the line above it ends
    ///     with, read from the <b>output</b>.
    /// </summary>
    /// <remarks>
    ///     ⚠ Read from the output and not from the input, and that is the whole of SK-FUZZ-0003.
    ///     <see cref="DefaultNewLine" /> answers with the first newline in the *input*, and the first
    ///     pass can move, rewrite or delete the text above that newline — so the second pass asks a
    ///     different question and gets a different answer:
    ///     <code>
    /// input   ␠␠&lt;LF&gt;using System;&lt;CRLF&gt;using System.Linq;&lt;LF&gt;
    /// pass 1  using System;&lt;CRLF&gt;using System.Linq;&lt;LF&gt;    ← the leading blank line is gone,
    /// pass 2  using System;&lt;CRLF&gt;using System.Linq;&lt;CRLF&gt;    so "the first newline" is now the CRLF
    /// pass 3  unchanged
    ///     </code>
    ///     `class C { // fuzz&lt;CRLF&gt;} &lt;CR&gt;` is the same story from the other side: the brace rule puts an
    ///     LF above the CRLF, and the final newline follows whichever ends up first.
    ///     <para>
    ///         The ending of the last break in the finished text is stable by construction — it is a
    ///         function of the output, so a second pass computes it from the text the first pass produced
    ///         and agrees. It also keeps what the input-reading version was *for*: a CRLF file still ends
    ///         CRLF and an LF file still ends LF, because the last break is the file's own.
    ///     </para>
    ///     <para>
    ///         ⚠ <c>enforce_line_ending_style = true</c> normalises every break to <c>end_of_line</c>, so
    ///         there is nothing to read and the configured ending is the answer.
    ///     </para>
    /// </remarks>
    static string FinalNewLine(string trimmed, in PhaseOneOptions options, string newLine) {
        if (options.EnforceLineEndingStyle) {
            return newLine;
        }

        var index = trimmed.LastIndexOfAny(['\n', '\r']);
        if (index < 0) {
            // A file with no line break at all: nothing has an opinion but the configuration.
            return newLine;
        }

        if (trimmed[index] == '\r') {
            return "\r";
        }

        return index > 0 && trimmed[index - 1] == '\r' ? "\r\n" : "\n";
    }

    /// <summary>
    ///     The ending a newly inserted break gets. ⚠ <c>enforce_line_ending_style = false</c> means
    ///     existing endings are preserved rather than normalised, so this is only consulted where the
    ///     source had no break to copy.
    /// </summary>
    /// <summary>
    ///     ⚠ A seam that makes the safety net refuse a named file, so that exit code 5 has a trigger.
    /// </summary>
    /// <remarks>
    ///     ⚠ This exists because <b>no input trips SK9099 any more</b>, and that is the good news it
    ///     looks like. All three that ever did are fixed and retired — SK-FUZZ-0001, SK-FUZZ-0005 and
    ///     SK-FUZZ-0002 — and a scan of all 1 520 files of <c>corpus/unformatted/</c>, the most
    ///     deliberately mangled input the project has, produces not one.
    ///     <para>
    ///         The row still has to be tested. docs/plan/09 gives 5 to "internal error", and
    ///         <c>ExitCodeContractTests.Five_WhenTheSafetyNetRefusesAFile</c> used SK-FUZZ-0002's
    ///         reproduction as its trigger — with a note saying that fixing the defect should give the test
    ///         a new trigger rather than delete it. This is that trigger. Forcing the refusal here rather
    ///         than faking an exit code keeps the whole downstream path real: the diagnostic's text,
    ///         <c>FormatCommand</c>'s failure counting, "a failed file outranks a changed one", and the code
    ///         the process returns.
    ///     </para>
    ///     <para>
    ///         ⚠ Matched on the file name rather than on merely being set, so that a run over a tree refuses
    ///         exactly one file and the "some failed, others changed" precedence is what gets exercised. Set
    ///         by the harness and by nobody else.
    ///     </para>
    /// </remarks>
    static string? ForcedVerificationFailure(string path) {
        var forced = Environment.GetEnvironmentVariable("SKALA_FORCE_SK9099");
        return !string.IsNullOrEmpty(forced)
            && string.Equals(System.IO.Path.GetFileName(path), forced, StringComparison.Ordinal)
                ? "(forced at token 0: 'A' became 'B')"
                : null;
    }

    static string DefaultNewLine(SourceText text, in PhaseOneOptions options) {
        if (options.EnforceLineEndingStyle) {
            return options.LineEnding switch {
                LineEnding.Crlf => "\r\n",
                LineEnding.Cr => "\r",
                _ => "\n"
            };
        }

        var content = text.ToString();
        var index = content.IndexOf('\n');
        if (index < 0) {
            return options.LineEnding == LineEnding.Crlf ? "\r\n" : "\n";
        }

        return index > 0 && content[index - 1] == '\r' ? "\r\n" : "\n";
    }
}

/// <summary>
///     ⚠ The formatter does not touch generated files (docs/plan/04 § "What the engine does not do").
/// </summary>
public static class GeneratedCode {
    public static bool IsGenerated(string path, SourceText text) {
        var name = System.IO.Path.GetFileName(path);
        if (name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        // Roslyn's own heuristic: `<auto-generated>` in the first comment block of the file.
        var limit = Math.Min(text.Lines.Count, 10);
        for (var i = 0; i < limit; i++) {
            var line = text.Lines[i].ToString();
            if (line.Contains("<auto-generated", StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }
}
