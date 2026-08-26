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
                lines += 1 + CSharpDocumentBuilder.CountNewLines(Original.ToString(
                    Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(edit.Span.Start, edit.Span.End)));
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

    /// <summary>Formats text that has already been read, with options that have already been resolved.</summary>
    public static FormatResult Format(string path, SourceText text, in FormattingOptions options, string? crashRoot = null) {
        var phaseOne = new PhaseOneOptions(options);
        return Format(path, text, phaseOne, crashRoot);
    }

    public static FormatResult Format(string path, SourceText text, in PhaseOneOptions options, string? crashRoot = null) {
        var diagnostics = ImmutableArray.CreateBuilder<SkalaDiagnostic>();

        if (GeneratedCode.IsGenerated(path, text)) {
            return new FormatResult(path, text, [], text.ToString(), diagnostics.ToImmutable(), FormatOutcome.Generated);
        }

        var tree = CSharpSyntaxTree.ParseText(text, ParseOptions, path);
        foreach (var diagnostic in tree.GetDiagnostics()) {
            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            // ⚠ ADR-003: a file that does not parse is reported and left byte-identical. This is the
            // single most important safety property in the tool.
            var position = diagnostic.Location.GetLineSpan().StartLinePosition;
            diagnostics.Add(new SkalaDiagnostic(
                FormatDiagnosticIds.NotParseable,
                SkalaSeverity.Warning,
                $"not formatted, the file does not parse: {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}",
                path,
                position.Line + 1));

            return new FormatResult(path, text, [], text.ToString(), diagnostics.ToImmutable(), FormatOutcome.NotParseable);
        }

        var built = CSharpDocumentBuilder.Build(path, text, tree.GetRoot(), options);
        diagnostics.AddRange(built.Diagnostics);

        var indentUnit = options.UseTabs ? "\t" : new string(' ', options.IndentSize);
        var newLine = DefaultNewLine(text, options);
        var layout = LayoutWriter.Write(
            built.Document,
            options.MaxLineLength,
            indentUnit,
            newLine,
            options.ContinuousIndentMultiplier);
        var output = ApplyFileLevelRules(layout.Text, options, newLine);
        var edits = EditEmitter.Emit(text.ToString(), layout with { Text = output });
        var formatted = EditEmitter.Apply(text.ToString(), edits);

        var after = SourceText.From(formatted, text.Encoding ?? System.Text.Encoding.UTF8);
        if (TokenEquivalence.Compare(text, after, ParseOptions) is { } failure) {
            var artefact = CrashArtifacts.Write(crashRoot, path, text.ToString(), formatted, options);
            diagnostics.Add(new SkalaDiagnostic(
                FormatDiagnosticIds.TokenStreamChanged,
                SkalaSeverity.Error,
                $"not written, the formatted output has a different token stream (at token {failure.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}: '{failure.Before}' became '{failure.After}')",
                path,
                0,
                artefact is null ? null : $"A reproduction is in {artefact}. This is a Skala bug; the file was left untouched."));

            return new FormatResult(path, text, [], text.ToString(), diagnostics.ToImmutable(), FormatOutcome.VerificationFailed);
        }

        return new FormatResult(path, text, [.. edits], formatted, diagnostics.ToImmutable(), FormatOutcome.Formatted);
    }

    /// <summary>Reads a file, resolves its options from the .editorconfig chain, and formats it.</summary>
    public static FormatResult FormatFile(string path, IReadOnlyList<KeyValuePair<string, string>>? overrides = null, string? crashRoot = null) {
        var text = Read(path);
        var resolution = OptionResolver.Resolve(path, overrides);
        return Format(path, text, resolution.Options, crashRoot);
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
