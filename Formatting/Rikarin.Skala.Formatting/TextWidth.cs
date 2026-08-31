using System.Globalization;

namespace Rikarin.Skala.Formatting;

/// <summary>
///     Column width, not character count.
/// </summary>
/// <remarks>
///     docs/plan/04 § "The fitting algorithm": a CJK identifier or an emoji in a string literal must
///     not silently blow the width budget, and a tab in the input is worth more than one column. Widths
///     are computed once at <c>Text</c> construction — recomputing during fitting turns a linear pass
///     quadratic (docs/plan/13).
/// </remarks>
public static class TextWidth {
    /// <summary>The tab stop used when measuring input that still contains tabs.</summary>
    public const int TabStop = 4;

    /// <summary>Columns occupied by <paramref name="value" /> starting at column 0.</summary>
    public static int Measure(string value) => Measure(value, 0);

    /// <summary>Columns occupied by <paramref name="value" /> when it starts at <paramref name="column" />.</summary>
    public static int Measure(string value, int column) => Advance(value, column) - column;

    /// <summary>
    ///     The column the cursor is at after writing <paramref name="value" /> from
    ///     <paramref name="column" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Not the same as <see cref="Measure(string, int)" /> plus the column, for text that spans
    ///     lines: a raw string literal ends at the width of its <em>last</em> line, not at the sum.
    ///     Milestone 1 assigned the width to the writer's column and the two happened to agree because
    ///     nothing read the column back; the fitting pass reads it on every group, and the mistake
    ///     showed up as a 126-column line the formatter thought was 72.
    /// </remarks>
    public static int Advance(string value, int column) {
        var enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext()) {
            var element = (string)enumerator.Current;
            if (element.Length == 1) {
                switch (element[0]) {
                    case '\t':
                        column += TabStop - column % TabStop;
                        continue;
                    case '\n':
                    case '\r':
                        // A multi-line Text (a raw string, a disabled #if block) restarts the count.
                        column = 0;
                        continue;
                }
            }

            column += ClusterWidth(element);
        }

        return column;
    }

    /// <summary>Columns for one grapheme cluster: 0 for combining marks, 2 for wide, 1 otherwise.</summary>
    static int ClusterWidth(string cluster) {
        var rune = char.ConvertToUtf32(cluster, 0);
        if (cluster.Length > 1 && char.IsHighSurrogate(cluster[0])) {
            return IsWide(rune) ? 2 : 1;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(cluster, 0);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) {
            return 0;
        }

        return IsWide(rune) ? 2 : 1;
    }

    /// <summary>The East Asian Wide and Fullwidth blocks, plus emoji.</summary>
    static bool IsWide(int rune) =>
        rune is >= 0x1100
            and <= 0x115F
            or >= 0x2E80
            and <= 0x303E
            or >= 0x3041
            and <= 0x33FF
            or >= 0x3400
            and <= 0x4DBF
            or >= 0x4E00
            and <= 0x9FFF
            or >= 0xA000
            and <= 0xA4CF
            or >= 0xAC00
            and <= 0xD7A3
            or >= 0xF900
            and <= 0xFAFF
            or >= 0xFE30
            and <= 0xFE6F
            or >= 0xFF00
            and <= 0xFF60
            or >= 0xFFE0
            and <= 0xFFE6
            or >= 0x1F300
            and <= 0x1F64F
            or >= 0x1F900
            and <= 0x1F9FF
            or >= 0x20000
            and <= 0x3FFFD;
}
