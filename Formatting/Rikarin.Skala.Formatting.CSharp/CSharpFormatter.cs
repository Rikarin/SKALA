using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Rikarin.Skala.Core.Configuration;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Formatting.CSharp;

/// <summary>What formatting one file produced.</summary>
public sealed record FormatResult(
    string Path,
    SourceText Original,
    ImmutableArray<TextEdit> Edits,
    string Formatted,
    ImmutableArray<SkalaDiagnostic> Diagnostics,
    FormatOutcome Outcome) {
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
/// The pipeline of docs/plan/04 § "The pipeline", minus the arrangement and fitting stages that
/// milestones 2–4 fill in.
/// </summary>
public static class CSharpFormatter {
    public static readonly CSharpParseOptions ParseOptions =
        new CSharpParseOptions(LanguageVersion.Preview).WithDocumentationMode(DocumentationMode.Parse);

    static readonly ConcurrentDictionary<string, CSharpParseOptions> SymbolisedOptions = new(StringComparer.Ordinal);

    /// <summary>
    /// The parse options for a file, given the preprocessor symbols that are defined for it.
    /// </summary>
    /// <remarks>
    /// ⚠ SK-DIV-0004. With no symbols Roslyn hands back every <c>#if DEBUG</c> body as
    /// <see cref="SyntaxKind.DisabledTextTrivia"/> — an unstructured string the formatter is not
    /// allowed to touch — while the oracle runs against a project where <c>DEBUG</c> is defined and
    /// formats it. Nothing else in the pipeline needs to change: which branch is disabled text is
    /// entirely a parse-time decision, so supplying the symbols is the whole fix.
    /// <para>
    /// Memoised because the symbol set is per-compilation and the file count per compilation is in
    /// the thousands; <see cref="CSharpParseOptions.WithPreprocessorSymbols(IEnumerable{string})"/>
    /// allocates a new options object and a new symbol map every call.
    /// </para>
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
        bool xmlDoc = false
    ) {
        var phaseOne = new PhaseOneOptions(options);
        return Format(
            path,
            text,
            phaseOne,
            crashRoot,
            preprocessorSymbols,
            xmlDoc ? new XmlDocOptions(options) : null
        );
    }

    /// <summary>
    /// ⚠ <paramref name="xmlDoc"/> is null unless <c>skala format --xmldoc</c> asked for the
    /// documentation-comment sub-formatter.
    /// </summary>
    /// <remarks>
    /// ⚠ Off by default, and that is a measurement rather than caution. <c>jb cleanupcode</c> does
    /// not format doc comments (SK-DIV-0006), so a Skala that re-wrapped them by default would
    /// disagree with Rider on every doc comment in every repository — which is the divergence
    /// SK-DIV-0009 spells out as "an option Skala honours and Rider ignores is a divergence wearing
    /// a tier badge". The flag has the same shape and the same reason as <c>arrange
    /// --aggressive</c> in SK-DIV-0014.
    /// </remarks>
    public static FormatResult Format(
        string path,
        SourceText text,
        in PhaseOneOptions options,
        string? crashRoot = null,
        IReadOnlyList<string>? preprocessorSymbols = null,
        XmlDocOptions? xmlDoc = null
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
            options.ContinuousIndentMultiplier
        );
        var output = ApplyFileLevelRules(layout.Text, options, newLine);

        // ⚠ After the layout and before anything measures or diffs it, because the sub-formatter
        // wraps against the *final* code indentation. Wrapping against the indentation the source
        // happened to have would make format(format(x)) differ from format(x) on every file whose
        // indentation the pipeline changed. See XmlDocFormatter.
        var reflowed = 0;
        if (xmlDoc is { } xml) {
            var outcome = XmlDocFormatter.Rewrite(output, xml, parseOptions, newLine);
            layout = XmlDocFormatter.Reanchor(layout, outcome.Text, outcome.Replacements);
            output = ApplyFileLevelRules(outcome.Text, options, newLine);
            reflowed = outcome.Reflowed;
        }

        ReportLongLines(path, output, options, diagnostics);
        var edits = EditEmitter.Emit(text.ToString(), layout with { Text = output });
        var formatted = EditEmitter.Apply(text.ToString(), edits);

        var after = SourceText.From(formatted, text.Encoding ?? System.Text.Encoding.UTF8);
        if (TokenEquivalence.Compare(text, after, parseOptions, reflowed > 0) is { } failure) {
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

        return new FormatResult(path, text, [.. edits], formatted, diagnostics.ToImmutable(), FormatOutcome.Formatted);
    }

    /// <summary>
    /// Reports every line the formatter could not fit, at <c>hint</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ docs/plan/04 § "The fitting algorithm": "Unfittable lines are left long. […] It never
    /// breaks a token, never breaks inside a string, and never emits a diagnostic for it by default
    /// (SK0002 at hint for the audit)." A 200-character string literal and a deeply-qualified
    /// generic type are not the formatter's to shorten, and a formatter that tried would be breaking
    /// tokens.
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
        bool xmlDoc = false
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
    /// Final newline and the trailing-whitespace sweep, applied last and as ordinary edits.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>resharper_csharp_insert_final_newline = true</c> wins over <c>[*] insert_final_newline
    /// = false</c> by language specificity (docs/plan/03, hazard 3). The BOM is preserved exactly:
    /// it lives in <see cref="SourceText.Encoding"/> and never in the text, so nothing here can add
    /// or remove one.
    /// </remarks>
    static string ApplyFileLevelRules(string output, in PhaseOneOptions options, string newLine) {
        var trimmed = output.TrimEnd(' ', '\t', '\r', '\n');
        if (trimmed.Length == 0) {
            return options.InsertFinalNewline && output.Length > 0 ? newLine : output;
        }

        return options.InsertFinalNewline ? trimmed + newLine : trimmed;
    }

    /// <summary>
    /// The ending a newly inserted break gets. ⚠ <c>enforce_line_ending_style = false</c> means
    /// existing endings are preserved rather than normalised, so this is only consulted where the
    /// source had no break to copy.
    /// </summary>
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
/// ⚠ The formatter does not touch generated files (docs/plan/04 § "What the engine does not do").
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
