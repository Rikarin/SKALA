namespace Rikarin.Skala.Formatting;

/// <summary>
///     The argument list width below which the oracle breaks after an <c>=</c> rather than chopping the
///     call's arguments: <see cref="GroupFacts.CalleeWidth" />'s floor (#446, SK-DIV-0211).
/// </summary>
/// <remarks>
///     ⚠ Tables, measured rather than derived, because no closed form survived: <c>var v… = Cccc(xxx, yyy);</c>
///     with the argument list one column at a time against the column of the call's <c>(</c> (52 to 112 one
///     column apart for callees of 7 and 20 columns; 65 to 110 three apart for callees of 1 to 40), and at
///     statement indents 8 to 24 inside nested blocks — 11 210 cells, each row one clean threshold, every one
///     reproduced here. The floor is the overflow itself up to a <c>(</c> near column 62 (every such row
///     chops), falls about a third of a column per column to 50 near column 86, and rises a fifth of one
///     after it; a longer callee shifts it slightly left, and each four columns of indent lower it by about
///     three. Between measured points the offsets are taken from the nearer one and the result floored.
/// </remarks>
/// <summary>The owners of an <c>=</c> <see cref="EqualsFloor" /> was measured under.</summary>
public enum EqualsOwner {
    /// <summary>Not measured: the ordering rule decides.</summary>
    None,

    /// <summary>A <c>var</c> local.</summary>
    VarLocal,

    /// <summary>A local with a written type.</summary>
    TypedLocal,

    /// <summary>A plain assignment statement.</summary>
    Assignment,

    /// <summary>A field declared at indent 4.</summary>
    Field
}

public static class EqualsFloor {
    /// <summary>The floor for a callee of 7 at indent 8, the <c>(</c> at columns 52 … 112.</summary>
    static readonly int[] Seven = [70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 58, 57, 57, 56, 56, 56, 55, 55, 55, 54, 54, 53, 53, 53, 52, 52, 51, 51, 51, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54, 55, 55, 55, 55, 55];

    /// <summary>The floor for a callee of 7 under a local with a written type, at indent 8, columns 52 … 112.</summary>
    static readonly int[] Typed = [70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 58, 58, 57, 57, 57, 56, 56, 55, 55, 55, 54, 54, 53, 53, 53, 52, 52, 52, 51, 51, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54, 55, 55, 55, 55, 55, 56, 56];

    /// <summary>The floor for a callee of 7 under an assignment statement, at indent 8, columns 52 … 112.</summary>
    static readonly int[] Assigned = [70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 56, 56, 55, 55, 55, 54, 54, 54, 53, 53, 52, 52, 52, 51, 51, 50, 50, 50, 49, 49, 49, 49, 50, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54];

    /// <summary>The floor for a callee of 7 under a field at indent 4, columns 52 … 112.</summary>
    static readonly int[] Field = [70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 56, 55, 54, 53, 52, 51, 50, 49, 48, 47, 46, 45, 44, 62, 61, 61, 61, 60, 60, 60, 60, 60, 60, 60, 61, 61, 61, 61, 61, 62, 62, 62, 62, 62, 63, 63, 63, 63, 63, 64, 64, 64, 64, 64, 65, 65, 65];

    /// <summary>The floor for a callee of 20 at indent 8, the <c>(</c> at columns 52 … 112.</summary>
    static readonly int[] Twenty = [70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 57, 57, 56, 56, 55, 55, 55, 54, 54, 54, 53, 53, 52, 52, 52, 51, 51, 50, 50, 50, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54, 55, 55, 55];

    /// <summary>The callee widths measured three columns apart, besides 7 and 20.</summary>
    static readonly int[] Callees = [1, 4, 10, 13, 16, 25, 30, 40];

    /// <summary>Their offset from <see cref="Seven" />, the <c>(</c> at columns 65, 68, … 110.</summary>
    static readonly int[][] CalleeOffsets = [
        [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0],
        [-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 0],
        [-1, 0, 0, 0, 0, 0, -1, 0, -1, 0, 0, -1, 0, -1, 0, 0],
        [-1, 0, 0, 0, 0, -1, -1, 0, -1, 0, 0, -1, 0, -1, 0, 0],
        [-1, -2, -3, -2, -1, -1, -1, 0, -1, 0, -1, -1, 0, -1, 0, -1],
    ];

    /// <summary>The floor for a callee of 7 at indents 8, 12, 16, 20 and 24, the <c>(</c> at 53, 56, … 110.</summary>
    static readonly int[][] Indents = [
        [69, 66, 63, 60, 58, 56, 55, 54, 53, 52, 51, 50, 51, 51, 52, 53, 53, 54, 54, 55],
        [69, 66, 63, 60, 57, 54, 52, 51, 50, 49, 48, 47, 48, 48, 49, 49, 50, 51, 51, 52],
        [69, 66, 63, 60, 57, 54, 51, 48, 47, 46, 45, 44, 45, 45, 46, 46, 47, 48, 48, 49],
        [69, 66, 63, 60, 57, 54, 51, 48, 45, 43, 42, 41, 42, 42, 43, 43, 44, 45, 45, 46],
        [69, 66, 63, 60, 57, 54, 51, 48, 45, 42, 39, 38, 39, 39, 40, 40, 41, 42, 42, 43]
    ];

    /// <summary>
    ///     The floor for a <c>(</c> at <paramref name="paren" /> (1-based), a statement at
    ///     <paramref name="indent" /> and a callee <paramref name="callee" /> columns wide.
    /// </summary>
    /// <param name="owner">
    ///     <see cref="EqualsOwner" />: which measured base the floor starts from. The callee's and the
    ///     indent's offsets were measured under a <c>var</c> local and are applied to every owner.
    /// </param>
    public static int Of(int paren, int indent, int callee, EqualsOwner owner = EqualsOwner.VarLocal) =>
        (int)Math.Floor(
            AtSeven(paren, owner)
            + CalleeOffset(paren, callee)
            + (owner == EqualsOwner.Field ? 0 : IndentOffset(paren, indent))
        );

    static int AtSeven(int paren, EqualsOwner owner) {
        var row = owner switch {
            EqualsOwner.TypedLocal => Typed,
            EqualsOwner.Assignment => Assigned,
            EqualsOwner.Field => Field,
            _ => Seven
        };

        return paren < 52 ? 122 - paren
            : paren > 112 ? row[^1] + (paren - 112) / 5
            : row[paren - 52];
    }

    static double CalleeOffset(int paren, int callee) {
        if (callee == 7) {
            return 0;
        }

        // Every measured callee width with its offset at this column, in order.
        Span<(int Width, double Offset)> rows = stackalloc (int, double)[Callees.Length + 2];
        var count = 0;
        var placedSeven = false;
        var placedTwenty = false;
        for (var i = 0; i <= Callees.Length; i++) {
            var width = i < Callees.Length ? Callees[i] : int.MaxValue;
            if (!placedSeven && 7 < width) {
                rows[count++] = (7, 0);
                placedSeven = true;
            }

            if (!placedTwenty && 20 < width) {
                rows[count++] = (20, TwentyOffset(paren));
                placedTwenty = true;
            }

            if (i < Callees.Length) {
                rows[count++] = (width, Nearest(CalleeOffsets[i], paren, 65));
            }
        }

        rows = rows[..count];
        if (callee <= rows[0].Width) {
            return rows[0].Offset;
        }

        for (var i = 0; i + 1 < rows.Length; i++) {
            if (callee <= rows[i + 1].Width) {
                var (lowWidth, low) = rows[i];
                var (highWidth, high) = rows[i + 1];
                return low + (high - low) * (callee - lowWidth) / (highWidth - lowWidth);
            }
        }

        return rows[^1].Offset;
    }

    static double TwentyOffset(int paren) =>
        paren < 52 || paren > 112 ? 0 : Twenty[paren - 52] - Seven[paren - 52];

    static double IndentOffset(int paren, int indent) {
        if (indent == 8) {
            return 0;
        }

        var row = Math.Clamp((indent - 8) / 4, 0, Indents.Length - 1);
        var beyond = indent - (8 + row * 4);
        var at = AtIndentRow(row, paren) - AtIndentRow(0, paren);
        if (beyond == 0) {
            return at;
        }

        if (row + 1 < Indents.Length && beyond > 0) {
            var next = AtIndentRow(row + 1, paren) - AtIndentRow(0, paren);
            return at + (next - at) * beyond / 4.0;
        }

        // Outside the measured indents: three columns per four of indent.
        return at - 3.0 * beyond / 4;
    }

    static double AtIndentRow(int row, int paren) =>
        paren <= 53 ? 122 - paren : Nearest(Indents[row], paren, 53);

    /// <summary>A row measured three columns apart from <paramref name="first" />, read at its nearest column.</summary>
    static double Nearest(int[] row, int paren, int first) {
        if (paren <= first) {
            return row[0];
        }

        var index = (paren - first + 1) / 3;
        return index >= row.Length ? row[^1] + (paren - (first + (row.Length - 1) * 3)) / 5.0 : row[index];
    }
}
