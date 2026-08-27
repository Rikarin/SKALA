using System.Text;

namespace Rikarin.Skala.Formatting;

/// <summary>One replacement against the ORIGINAL text (ADR-005).</summary>
public readonly record struct TextEdit(SourceSpan Span, string NewText) {
    public override string ToString() =>
        $"{Span} -> {NewText.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)}";
}

/// <summary>
/// Turns a written layout into the smallest edit list that produces it.
/// </summary>
/// <remarks>
/// docs/plan/04 § "Emitting minimal edits": walk the resolved layout and the original text in
/// lockstep, using <see cref="AnchorPoint"/>s as sync points. For each maximal region where output
/// bytes equal input bytes, emit nothing.
/// <para>
/// ⚠ The property that matters is the negative one: a region whose output bytes equal its input
/// bytes produces no edit. That is what keeps a first run on a 1.35 M-line tree reviewable.
/// </para>
/// </remarks>
public static class EditEmitter {
    public static IReadOnlyList<TextEdit> Emit(string input, Layout layout) {
        var edits = new List<TextEdit>();
        var output = layout.Text;
        var anchors = layout.Anchors;

        var inputCursor = 0;
        var outputCursor = 0;

        for (var i = 0; i < anchors.Count; i++) {
            var anchor = anchors[i];

            // ⚠ Clamped, and this is SK-FUZZ-0001. `layout.Text` is not always the text the anchors
            // were written against: `CSharpFormatter.Format` runs the file-level rules — the
            // trailing-whitespace trim and the inserted final newline — over the whole output
            // *after* the writer produced it, and both can make it shorter than the last anchor
            // claims. Normally nothing notices, because the last anchor ends before the trailing
            // whitespace. A `@formatter:off` span running to the end of the file covers it, and the
            // emitter then indexed past the end of the string and threw IndexOutOfRangeException out
            // of the process on a 32-byte input.
            var sourceStart = Math.Clamp(anchor.Source.Start, 0, input.Length);
            var sourceEnd = Math.Clamp(anchor.Source.End, sourceStart, input.Length);
            var outputStart = Math.Clamp(anchor.OutputStart, 0, output.Length);
            var outputEnd = Math.Clamp(anchor.OutputEnd, outputStart, output.Length);

            // The gap between the previous piece and this one.
            AddIfDifferent(edits, input, output, inputCursor, sourceStart, outputCursor, outputStart);

            // The piece itself. In phase 1 a piece is copied verbatim, so this is normally a no-op;
            // it is compared anyway so that a builder that ever rewrites a token cannot lose it.
            AddIfDifferent(edits, input, output, sourceStart, sourceEnd, outputStart, outputEnd);

            inputCursor = sourceEnd;
            outputCursor = outputEnd;
        }

        AddIfDifferent(edits, input, output, inputCursor, input.Length, outputCursor, output.Length);
        return edits;
    }

    static void AddIfDifferent(
        List<TextEdit> edits,
        string input,
        string output,
        int inputStart,
        int inputEnd,
        int outputStart,
        int outputEnd
    ) {
        // ⚠ The second half of the same guard as the clamp in Emit, kept here as well because this
        // is the method that indexes the two strings and a caller that gets the arithmetic wrong
        // should produce no edit rather than a stack trace at the user.
        if (inputEnd < inputStart
            || outputEnd < outputStart
            || inputStart < 0
            || outputStart < 0
            || inputEnd > input.Length
            || outputEnd > output.Length) {
            return;
        }

        var inputLength = inputEnd - inputStart;
        var outputLength = outputEnd - outputStart;
        if (inputLength == outputLength
            && string.CompareOrdinal(input, inputStart, output, outputStart, inputLength) == 0) {
            return;
        }

        // Trim the common prefix and suffix so the edit spans the smallest range that differs.
        var prefix = 0;
        var max = Math.Min(inputLength, outputLength);
        while (prefix < max && input[inputStart + prefix] == output[outputStart + prefix]) {
            prefix++;
        }

        var suffix = 0;
        while (suffix < max - prefix && input[inputEnd - 1 - suffix] == output[outputEnd - 1 - suffix]) {
            suffix++;
        }

        edits.Add(
            new TextEdit(
                SourceSpan.FromBounds(inputStart + prefix, inputEnd - suffix),
                output[(outputStart + prefix)..(outputEnd - suffix)]
            )
        );
    }

    /// <summary>Applies edits to <paramref name="input"/>. Edits must be ordered and disjoint.</summary>
    public static string Apply(string input, IReadOnlyList<TextEdit> edits) {
        if (edits.Count == 0) {
            return input;
        }

        var builder = new StringBuilder(input.Length);
        var cursor = 0;
        foreach (var edit in edits) {
            builder.Append(input, cursor, edit.Span.Start - cursor);
            builder.Append(edit.NewText);
            cursor = edit.Span.End;
        }

        builder.Append(input, cursor, input.Length - cursor);
        return builder.ToString();
    }

    /// <summary>
    /// <c>--range a:b</c>: the edits that intersect a range, filtered AFTER full-file fitting, which
    /// is the only way range formatting can be consistent with whole-file formatting.
    /// </summary>
    public static IReadOnlyList<TextEdit> Restrict(IReadOnlyList<TextEdit> edits, SourceSpan range) =>
        [.. edits.Where(edit => edit.Span.IntersectsWith(range))];
}
