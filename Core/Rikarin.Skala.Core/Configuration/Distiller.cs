using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Rikarin.Skala.Options;

namespace Rikarin.Skala.Core.Configuration;

public sealed record DistillResult(
    string Text,
    int LinesIn,
    int LinesOut,
    int Dropped,
    int RetainedUnverifiedDefault,
    ImmutableArray<string> DroppedKeys);

/// <summary>
/// Rewrites a Rider export as the subset that differs from ReSharper's defaults.
/// </summary>
/// <remarks>
/// ⚠ The rule in <see cref="ShouldDrop"/> is the whole safety argument. A key may only be dropped
/// when the registry's default for it was <em>checked</em> — against JetBrains' documentation, or
/// against the oracle. Dropping a key because the default *looks* like the configured value silently
/// changes formatting, and a formatter that silently changes formatting is worse than no formatter,
/// because it is trusted.
///
/// ⚠ Until milestone 3 this dropped nothing at all, and the reason was not a bug: JetBrains'
/// EditorConfig property tables publish names, languages and possible values and no defaults, so no
/// entry could be <see cref="OptionDefaultSource.ReSharperDocs"/> and the rule had nothing to fire
/// on. M3 derived the defaults from the oracle instead — a run under a configuration carrying
/// nothing but <c>root = true</c> is ReSharper-with-defaults by construction — and
/// <see cref="OptionDefaultSource.OracleProbe"/> is the resulting evidence class. It is a strong
/// signal rather than proof, which is exactly the standard a key has to meet before it may be
/// deleted from somebody's configuration.
/// </remarks>
public static class Distiller {
    public static bool ShouldDrop(OptionInfo info, string value) =>
        info.DefaultSource is OptionDefaultSource.ReSharperDocs or OptionDefaultSource.OracleProbe
        && info.Default is not null
        && string.Equals(Normalize(info, value), Normalize(info, info.Default), StringComparison.Ordinal);

    static string Normalize(OptionInfo info, string value) {
        var text = value.Trim();
        if (!info.SeveritySuffix) {
            return text;
        }

        var colon = text.LastIndexOf(':');
        return colon < 0 ? text : text[..colon].Trim();
    }

    public static DistillResult Distill(EditorConfigDocument document) {
        var dropByLine = new HashSet<int>();
        var droppedKeys = ImmutableArray.CreateBuilder<string>();
        var retained = 0;

        foreach (var section in document.Sections) {
            foreach (var assignment in section.Assignments) {
                if (!OptionRegistry.TryResolve(assignment.Key, out var id)) {
                    continue;
                }

                var info = OptionRegistry.Get(id);
                if (ShouldDrop(info, assignment.Value)) {
                    dropByLine.Add(assignment.Line);
                    droppedKeys.Add(assignment.Key);
                } else if (info.DefaultSource is not (OptionDefaultSource.ReSharperDocs
                               or OptionDefaultSource.OracleProbe)) {
                    retained++;
                }
            }
        }

        var newline = document.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var builder = new StringBuilder();
        var linesIn = 0;
        var linesOut = 0;
        var line = 0;

        builder.Append("# Distilled by skala config distill.").Append(newline);
        builder.Append("# Every key below either differs from ReSharper's default or has a default Skala could not")
            .Append(newline);
        builder.Append("# verify. Re-exporting from Rider over this file is safe and supported (ADR-001).")
            .Append(newline);

        // ⚠ A comment stuck to a dropped key goes with it.
        //
        // Dropping the assignment and leaving the comment above it produces a file describing a key
        // that is no longer there. In the Rider export the comments are section headers, so the
        // damage is cosmetic; in a hand-annotated configuration it is actively misleading text, and
        // producing a file a human can read is the entire purpose of `distill`.
        //
        // The semantics are `resharper_stick_comment`'s, which this project already settled for
        // code: a contiguous run of comment lines belongs to the single line directly beneath it.
        // So a run followed by a blank line, by a section header, or by nothing is attached to no
        // key and stays — which is what keeps a file's header and its section banners intact.
        var stuck = new List<string>();

        void Flush() {
            foreach (var comment in stuck) {
                builder.Append(comment).Append(newline);
                linesOut++;
            }

            stuck.Clear();
        }

        foreach (var raw in document.Text.Split('\n')) {
            line++;
            linesIn++;
            var text = raw.TrimEnd('\r');
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('#') || trimmed.StartsWith(';')) {
                stuck.Add(text);
                continue;
            }

            if (dropByLine.Contains(line)) {
                stuck.Clear();
                continue;
            }

            Flush();
            builder.Append(text).Append(newline);
            linesOut++;
        }

        // ⚠ Trailing comments are attached to nothing and are kept. A file that ends in a note
        // about why the rest of it looks the way it does must not lose the note.
        Flush();

        return new DistillResult(
            builder.ToString(),
            linesIn,
            linesOut + 3,
            dropByLine.Count,
            retained,
            droppedKeys.ToImmutable()
        );
    }

    public static string Summary(DistillResult result) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{result.LinesIn} lines in, {result.LinesOut} out; {result.Dropped} key(s) dropped as equal to a verified ReSharper default, {result.RetainedUnverifiedDefault} retained because their default is unverified"
        );
}
