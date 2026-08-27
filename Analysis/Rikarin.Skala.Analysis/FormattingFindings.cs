using System.Collections.Immutable;
using Rikarin.Skala.Core.Diagnostics;
using Rikarin.Skala.Formatting.CSharp;
using Rikarin.Skala.Reporting;
using Rikarin.Skala.Rules.Metadata;

namespace Rikarin.Skala.Analysis;

/// <summary>
/// The <c>SK0xxx</c> half of the report: what the formatter would change, as findings with fixes.
/// </summary>
/// <remarks>
/// ⚠ Not an analyzer. <c>SK0001</c> cannot be a <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/>
/// because its answer is "run the whole formatter over this file and see", which is a document
/// build and a fitting pass rather than a syntax-node visit — and because its fix is the formatter's
/// own <c>TextChange</c> list, which is already exactly what a SARIF <c>artifactChange</c> is.
/// <para>
/// ⚠ It carries the real edits rather than a message telling the reader to run the formatter. That
/// is what lets an agent apply the whole report — formatting and modernization together — in one
/// pass, and it is why formatting is in the report at all instead of being a separate command with
/// a separate exit code.
/// </para>
/// </remarks>
public static class FormattingFindings {
    public static ImmutableArray<Finding> Collect(
        string repositoryRoot,
        IReadOnlyList<string> paths,
        CheckRequest request,
        ImmutableArray<SkalaDiagnostic>.Builder diagnostics
    ) {
        var files = paths.Where(static path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0) {
            return [];
        }

        var results = new FormatResult?[files.Length];
        var crashRoot = Path.Combine(repositoryRoot, ".skala");
        Parallel.For(
            0,
            files.Length,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 10) },
            index => {
                try {
                    results[index] = CSharpFormatter.FormatFile(
                        files[index],
                        request.Overrides,
                        crashRoot,
                        request.Define
                    );
                } catch (IOException) {
                    results[index] = null;
                }
            }
        );

        var findings = ImmutableArray.CreateBuilder<Finding>();
        for (var i = 0; i < files.Length; i++) {
            var result = results[i];
            if (result is null) {
                continue;
            }

            foreach (var diagnostic in result.Diagnostics) {
                // ⚠ SK0002 and SK0003 are findings; SK9010, SK9011 and SK9099 are the tool talking
                // about itself and belong in the invocation's notifications, not in results[].
                if (diagnostic.Id is FormatDiagnosticIds.LineTooLong or FormatDiagnosticIds.MalformedXmlDoc) {
                    findings.Add(FromDiagnostic(diagnostic, files[i], result));
                } else {
                    diagnostics.Add(diagnostic);
                }
            }

            if (result.Outcome != FormatOutcome.Formatted || result.Edits.IsEmpty) {
                continue;
            }

            var line = result.Original.Lines.GetLinePosition(result.Edits[0].Span.Start);
            findings.Add(
                new Finding {
                    RuleId = RuleIds.FileIsNotFormatted,
                    Severity = SkalaSeverity.Info,
                    Message = $"the file is not formatted ({result.Edits.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} edit(s)); run `skala format`",
                    Path = files[i],
                    Line = line.Line + 1,
                    Column = line.Character + 1,
                    EndLine = line.Line + 1,
                    EndColumn = line.Character + 1,
                    Start = result.Edits[0].Span.Start,
                    Length = 0,
                    Fix = [
                        .. result.Edits.Select(edit =>
                            new FixEdit(files[i], edit.Span.Start, edit.Span.End - edit.Span.Start, edit.NewText)
                        )
                    ],
                    FixIsSafe = true
                }
            );
        }

        return findings.ToImmutable();
    }

    static Finding FromDiagnostic(SkalaDiagnostic diagnostic, string path, FormatResult result) {
        var offset = diagnostic.Line > 0 && diagnostic.Line <= result.Original.Lines.Count
            ? result.Original.Lines[diagnostic.Line - 1].Start
            : 0;

        return new Finding {
            RuleId = diagnostic.Id,
            Severity = diagnostic.Severity,
            Message = diagnostic.Message,
            Path = path,
            Line = Math.Max(1, diagnostic.Line),
            Column = 1,
            EndLine = Math.Max(1, diagnostic.Line),
            EndColumn = 1,
            Start = offset,
            Length = 0
        };
    }
}
