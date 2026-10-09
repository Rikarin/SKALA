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
    /// <summary>
    ///     The column a sole lambda argument's arrow has to reach for it to break over an operand-chain or
    ///     binary-pattern body (#578): the larger of 20 and the smaller of a ceiling the parameters and the
    ///     first operand set and a column that rises with the line's end.
    /// </summary>
    /// <param name="parameters">The width of everything before ` =&gt;`, modifiers and parentheses included.</param>
    /// <param name="first">The body's first operand's width.</param>
    /// <param name="end">The column the whole line would end at.</param>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on <c>U(params =&gt; a… &amp;&amp; b… &amp;&amp; c…);</c>,
    ///     <c>
    ///         … =&gt; x
    ///         is A or B or C…
    ///     </c>
    ///     , <c>var g = i….Where(params =&gt; …);</c> and <c>static</c> lambdas: parameter texts of
    ///     1 to 45 columns, first operands of 6 to 52, the arrow at columns 14 to 95 and lines of 112 to 200, 31 671
    ///     cells. A modifier counts as parameter text: <c>static n</c> decides as an eight-column name. ⚠ The ceiling
    ///     rises 2.75 columns per column of parameters and of first operand up to 24, then about one per eight, and
    ///     never past where the first operand still fits beside the arrow; the end's column moves 0.9 per column of
    ///     line and 0.3 per column of parameters, three and a quarter later once the first operand passes 18. 28 cells
    ///     under parameter texts of 36 differ, all within a column of the boundary; past 36 the oracle is not monotone
    ///     and the rule is not measured.
    /// </remarks>
    /// <param name="patternLeft">
    ///     ⚠ For a body that is <c>left is A or B …</c> rather than an operand chain, the width of <c>left</c>; zero
    ///     for a chain (#586). The pattern's constants are its own. With <c>t = min(first, left + 28)</c> — the
    ///     first operand, its type capped at 24 columns — the ceiling is
    ///     <c>⌊2.75·(params + t) − 56 − 0.88·(left − 1) + (first − t)/8⌋</c>, and the line column has no
    ///     first-operand bonus but a deficit, <c>max(0, 10 + left − 0.375·t)</c>: a wide tested expression lowers
    ///     both, a wide type raises both until it passes 24. Measured with <c>Testing ask</c> on
    ///     <c>U(params =&gt; left is A… or B… or C…);</c> and <c>var g = i….Where(…);</c>: parameter texts of 1 to
    ///     30, tested expressions of 1 to 12, first operands of 8 to 58, the arrow at 15 to 98 one column at a time
    ///     at a 200-column line and at lines of 116 to 176 — 30 817 cells, of which the operand chain's constants
    ///     missed 2 433 and these miss 49 — and validated on 3 781 random cells (ten parameter texts, eight tested
    ///     expressions, types of 3 to 40, lines of 118 to 200, either context, the remaining operands split at
    ///     random), where the chain's constants miss 540 and these 15.
    /// </param>
    public static bool BreaksTheOperandArrow(int arrow, int parameters, int first, int end, int patternLeft = 0) {
        if (patternLeft > 0) {
            var capped = Math.Min(first, patternLeft + 28);
            var patternCeiling = Math.Min(
                85,
                Math.Min(
                    (int)Math.Floor(
                        2.75 * (parameters + capped) - 56 - 0.88 * (patternLeft - 1) + (first - capped) / 8.0
                    ),
                    120 - first
                )
            );
            var patternLine = (27.0 * end + 9 * parameters - 2832) / 30
                - Math.Max(0, 10 + patternLeft - 0.375 * capped);
            return arrow >= Math.Max(20, Math.Min(patternCeiling, patternLine));
        }

        var ceiling = Math.Min(
            85,
            Math.Min(
                (int)Math.Floor(2.75 * (parameters + Math.Min(first, 24)) - 52 + Math.Max(0, first - 24) / 8.0),
                120 - first
            )
        );
        var line = (27.0 * end + 9 * parameters - 2832) / 30 + Math.Min(3.25, Math.Max(0, 0.625 * (first - 18)));
        return arrow >= Math.Max(20, Math.Min(ceiling, line));
    }


    /// <summary>
    ///     Whether a local's lambda over a call with two or more arguments breaks its arrow past the margin, rather
    ///     than keeping it and chopping the call's arguments (#453): the <c>=</c> ends at column
    ///     <paramref name="head" />, the call's <c>(</c> stands at <paramref name="paren" /> and its argument list
    ///     is <paramref name="arguments" /> wide.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured with <c>Testing ask</c> on <c>Func&lt;T…&gt; n = () =&gt; C…(xxx, yyy);</c>, heads of 16 to 76
    ///     four apart, the <c>(</c> and the argument list one column at a time — 53 681 cells, every row one clean
    ///     threshold — and the head alone decides under a name of nine columns or fewer: the same head made of a
    ///     wider type or a wider name reads the same row. The arrow breaks while the arguments are narrower than
    ///     a floor that falls 0.4 a column from 65 where the <c>(</c> is 38 columns past the head, and rises 0.18 a
    ///     column after, 0.41 higher per column of head; with the <c>(</c> nearer the head than
    ///     <c>min(38, 62 − head/2)</c> columns the arguments always chop. 45 cells differ, within a column of
    ///     the floor. Validated on 10 225 cells of names of 1, 5 and 9 under six types (4 differ), and on 4 000
    ///     random cells — <c>var</c>, <c>Action</c>, <c>Func&lt;…&gt;</c> and bare types, six parameter lists, two
    ///     to four arguments: 9 of 3 173 differ. ⚠ A single argument does not follow it (half of 827 cells
    ///     differ) and is left to the head rule.
    /// </remarks>
    /// <param name="oneArgument">
    ///     ⚠ The call has one argument (#453). Its floor is the same two lines raised by 21.25 columns, falling
    ///     0.41 a column, with no column below which the arguments always chop: measured on the same grid with a
    ///     one-name argument, 53 602 cells, 60 within a column of the floor differ.
    /// </param>
    public static bool BreaksTheCallArrow(int head, int paren, int arguments, bool oneArgument = false) {
        if (oneArgument) {
            var oneFall = 65 - 0.41 * (paren - head - 38);
            var oneRise = 58.25 + 0.41 * (head - 16) + 0.18 * (paren - 117);
            return arguments < Math.Floor(Math.Max(oneFall, oneRise) + 21.25 + 0.3);
        }

        if (paren < Math.Min(head + 38, 62 + head / 2.0)) {
            return false;
        }

        var fall = 65 - 0.4 * (paren - head - 38);
        var rise = 58.25 + 0.41 * (head - 16) + 0.18 * (paren - 117);
        return arguments < Math.Floor(Math.Max(fall, rise) + 0.4);
    }

    /// <summary>
    ///     Whether a local's lambda with a bare-name body, on a line that ends exactly one column past the
    ///     margin, chops its parameter list — <c>name = (</c> / parameters / <c>) =&gt; body;</c> — rather
    ///     than breaking its arrow or its <c>=</c> (#572). The declaration's type is <paramref name="type" />
    ///     wide, the body <paramref name="body" /> and the <c>=</c> ends at <paramref name="head" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on 6 058 cells of <c>Func&lt;T…&gt; name = (P… p0) =&gt; body;</c> ending at 121: types of
    ///     8 to 46 columns (two apart), bodies of 1 to 9, heads of 16 to 79 one column at a time. The
    ///     parameters chop exactly up to a head that falls two to three columns per column of body and rises
    ///     about one and a quarter per column of type until it stops at a type of 32. A body of eight chops
    ///     them at a head of 49 only, under types of 26 to 36; a body of nine never does. At 122 and 123 no
    ///     cell chops. An odd type width reads the row below it, which was not measured. ⚠ Past the run,
    ///     from a type of 32, the oracle breaks between the type and the name instead of after the
    ///     <c>=</c>, which is not this rule's and not wired.
    /// </remarks>
    public static bool ChopsOneOver(int type, int body, int head) {
        if (body is < 1 or > 8) {
            return false;
        }

        var row = Math.Clamp((type - 8) / 2, 0, OneOverHeads.Length - 1);

        // ⚠ A body of eight chops at one head only, where the arrow hands over to the `=`.
        return body == 8 ? head == OneOverHeads[row][7] : head <= OneOverHeads[row][body - 1];
    }

    /// <summary>The widest head that chops at 121, by type (8, 10 … 46) and body (1 … 8); 0 never.</summary>
    static readonly int[][] OneOverHeads = [
        [45, 42, 40, 38, 37, 35, 33, 0],
        [44, 42, 40, 38, 37, 35, 33, 0],
        [45, 43, 41, 40, 38, 36, 35, 0],
        [47, 45, 44, 42, 40, 39, 37, 0],
        [50, 48, 46, 44, 43, 41, 39, 0],
        [52, 50, 48, 47, 45, 43, 41, 0],
        [55, 52, 51, 49, 47, 45, 44, 0],
        [57, 55, 53, 51, 49, 48, 46, 0],
        [60, 57, 55, 53, 52, 50, 48, 0],
        [62, 59, 57, 56, 54, 52, 51, 49],
        [65, 62, 60, 58, 56, 55, 52, 49],
        [67, 64, 62, 60, 58, 55, 52, 49],
        [69, 66, 63, 60, 58, 55, 52, 49],
        [69, 66, 63, 60, 58, 55, 52, 49],
        [69, 66, 63, 60, 58, 55, 52, 49],
        [69, 66, 63, 60, 58, 55, 52, 0],
        [69, 66, 63, 60, 57, 55, 52, 0],
        [69, 66, 63, 60, 57, 55, 52, 0],
        [68, 66, 63, 60, 57, 55, 52, 0],
        [68, 66, 63, 60, 57, 55, 52, 0]
    ];

    /// <summary>
    ///     The narrowest value — the lambda from its <c>(</c> through the statement's <c>;</c> — that keeps
    ///     an <c>=</c> on its line when the <c>=</c> ends at column <paramref name="head" /> and the line
    ///     through the lambda's <c>=&gt;</c> fits (#558). A narrower value moves below the <c>=</c> whole.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on 1 723 cells of <c>Func&lt;A, B&gt; f = (…) =&gt; body;</c>:
    ///     <list type="bullet">
    ///         <item>heads of 22 to 75;</item>
    ///         <item>values of 74 to 93 columns, one column at a time;</item>
    ///         <item>bodies of 4, 5, 12, 20 and 30 columns;</item>
    ///         <item>one long parameter type, and runs of short parameters, which decide alike.</item>
    ///     </list>
    ///     The floor is 88 up to a head of 44 and falls about a column per two and a half of head after it.
    ///     Two cells with a 30-column body, at heads 58 and 61, are a column off this row. Past 59 it is
    ///     extrapolated from the bound the measured rows give.
    /// </remarks>
    public static int LambdaValue(int head) =>
        head <= 44 ? 88
        : head <= 59 ? LambdaValueRow[head - 45]
        : 82 - (head - 59) / 3;

    static readonly int[] LambdaValueRow = [87, 87, 87, 86, 86, 85, 85, 85, 84, 84, 84, 83, 83, 82, 82];

    /// <summary>
    ///     Whether an <c>=</c> breaks before a lambda with a bare-name body whose line through <c>=&gt;</c>
    ///     overflows by <paramref name="over" /> columns, rather than the lambda's parameter list chopping
    ///     (#558). The <c>=</c> ends at <paramref name="head" />; the value from <c>(</c> through <c>;</c>
    ///     is <paramref name="value" /> wide and the body <paramref name="body" />.
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on 2 916 cells: heads of 22 to 62, bodies of 5, 8, 12, 16, 20, 25, 30 and 40 columns,
    ///     and the <c>=&gt;</c> ending 121 to 141. The <c>=</c> breaks while the
    ///     <c>=&gt;</c> ends at most three columns past the margin — the <c>)</c> still on the line — and
    ///     past that while the value is at most <see cref="LambdaValue" />'s floor plus
    ///     <c>⌊(5·body − 41) / 3⌋</c>. Below it the value goes whole onto the next line, or breaks
    ///     after its arrow there; above it the parameter list chops. 15 cells are a column off this
    ///     boundary, all at heads of 44 or more: the measured boundary itself has a column of jitter there.
    ///     ⚠ Past a name no wider than <see cref="LambdaLocal.ChopsPastTheParenthesis" />'s gate the
    ///     parameter list always chops once the <c>)</c> is off the line (<paramref name="narrow" />).
    /// </remarks>
    public static bool BreaksBeforeAnOverflowingLambda(int head, int value, int over, int body, bool narrow) =>
        over <= 3 || !narrow && value <= LambdaValue(head) + (int)Math.Floor((5 * body - 41) / 3.0);

    /// <summary>
    ///     Whether a local's <c>=</c> breaks before <c>operand is A or B</c> on a line ending at
    ///     <paramref name="end" />, the head <paramref name="head" /> columns wide through the <c>=</c> and the
    ///     binary pattern <paramref name="pattern" /> wide (#446, SK-DIV-0211).
    /// </summary>
    /// <remarks>
    ///     ⚠ Measured on 2 336 cells — heads of 8 to 60, patterns of 10 to 75 columns (two names joined by
    ///     <c>or</c>, and <c>&gt; 5 and &lt; 10</c>, <c>null or Empty</c>, three names), lines of 110 to 140.
    ///     A head of 12 or more always breaks the <c>=</c>, whatever the pattern; a narrower head breaks it
    ///     only for a pattern under a width that grows with the head, and for the two widths just past
    ///     that, only once the line reaches a column that moves five per column of pattern. One cell — a
    ///     head of 11, a pattern of 39, a line of 124 — keeps the pattern where both of its neighbours break
    ///     the <c>=</c>, and it is in the table as measured.
    /// </remarks>
    public static bool BreaksBeforeAPattern(int head, int pattern, int end) {
        if (head >= 12) {
            return pattern <= WidestPatternAfterAWideHead(head, end);
        }

        // Per head 8 … 11: the widest pattern that always breaks the `=`, then the line ends from which the
        // next two widths do.
        var row = Math.Clamp(head, 8, 11) - 8;
        ReadOnlySpan<int> always = [27, 31, 35, 38];
        ReadOnlySpan<int> firstFrom = [125, 123, 126, 121];
        ReadOnlySpan<int> secondFrom = [130, 128, 131, 132];
        if (pattern <= always[row]) {
            return true;
        }

        if (pattern == always[row] + 1) {
            return end >= firstFrom[row] && !(head == 11 && pattern == 39 && end == 124);
        }

        return pattern == always[row] + 2 && end >= secondFrom[row];
    }

    /// <summary>Heads of 12 … 40 measured, and the line ends 121 … 152 one apart.</summary>
    static readonly int[] PatternHeads = [12, 14, 16, 20, 24, 30, 40];

    /// <summary>
    ///     The widest pattern before which a head of 12 or more still breaks the <c>=</c> (5 847 cells, every
    ///     row one threshold): a wider one is chopped on the declaration's line instead. 90 where the row was
    ///     all <c>=</c> breaks as far as measured, the widest any row reached.
    /// </summary>
    static readonly int[][] WidestPattern = [
        [
            88, 87, 87, 87, 86, 86, 86, 85, 85, 85, 84, 84, 83, 83, 83, 82, 82, 82, 81, 81, 81, 80, 80, 80, 79, 79, 79,
            78, 78, 77, 77, 77
        ],
        [
            89, 88, 88, 88, 87, 87, 87, 86, 86, 86, 85, 85, 84, 84, 84, 83, 83, 83, 82, 82, 82, 81, 81, 81, 80, 80, 80,
            79, 79, 78, 78, 78
        ],
        [
            90, 89, 89, 89, 88, 88, 88, 87, 87, 87, 86, 86, 86, 85, 85, 84, 84, 84, 83, 83, 83, 82, 82, 82, 81, 81, 81,
            80, 80, 80, 79, 79
        ],
        [
            90, 90, 90, 90, 90, 90, 90, 89, 89, 89, 88, 88, 88, 87, 87, 87, 86, 86, 85, 85, 85, 84, 84, 84, 83, 83, 83,
            82, 82, 82, 81, 81
        ],
        [
            90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 89, 89, 89, 88, 88, 88, 87, 87, 86, 86, 86, 85, 85, 85,
            84, 84, 84, 83, 83
        ],
        [
            90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 91, 91, 90, 90, 89, 89, 89, 88, 88, 88, 87,
            87, 87, 86, 86, 86
        ],
        [
            90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 90, 88, 88, 88,
            87, 87, 87, 86, 86
        ]
    ];

    static int WidestPatternAfterAWideHead(int head, int end) {
        var row = 0;
        while (row < PatternHeads.Length - 1 && head >= PatternHeads[row + 1]) {
            row++;
        }

        // The measured row at or below this head: a wider head only moves the threshold up.
        var values = WidestPattern[row];
        return end <= 121 ? values[0]
            : end >= 152 ? values[^1] - (end - 152) / 3
            : values[end - 121];
    }

    /// <summary>The floor for a callee of 7 at indent 8, the <c>(</c> at columns 52 … 112.</summary>
    static readonly int[] Seven = [
        70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 58, 57, 57, 56, 56, 56, 55, 55, 55, 54, 54, 53, 53, 53, 52,
        52, 51, 51, 51, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54,
        55, 55, 55, 55, 55
    ];

    /// <summary>The floor for a callee of 7 under a local with a written type, at indent 8, columns 52 … 112.</summary>
    static readonly int[] Typed = [
        70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 58, 58, 57, 57, 57, 56, 56, 55, 55, 55, 54, 54, 53, 53, 53,
        52, 52, 52, 51, 51, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54, 54, 54, 55, 55,
        55, 55, 55, 56, 56
    ];

    /// <summary>The floor for a callee of 7 under an assignment statement, at indent 8, columns 52 … 112.</summary>
    static readonly int[] Assigned = [
        70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 56, 56, 55, 55, 55, 54, 54, 54, 53, 53, 52, 52, 52, 51,
        51, 50, 50, 50, 49, 49, 49, 49, 50, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53,
        54, 54, 54, 54, 54
    ];

    /// <summary>The floor for a callee of 7 under a field at indent 4, columns 52 … 112.</summary>
    static readonly int[] Field = [
        70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 56, 55, 54, 53, 52, 51, 50, 49, 48, 47, 46, 45, 44, 62,
        61, 61, 61, 60, 60, 60, 60, 60, 60, 60, 61, 61, 61, 61, 61, 62, 62, 62, 62, 62, 63, 63, 63, 63, 63, 64, 64, 64,
        64, 64, 65, 65, 65
    ];

    /// <summary>The floor for a callee of 20 at indent 8, the <c>(</c> at columns 52 … 112.</summary>
    static readonly int[] Twenty = [
        70, 69, 68, 67, 66, 65, 64, 63, 62, 61, 60, 59, 58, 57, 57, 57, 56, 56, 55, 55, 55, 54, 54, 54, 53, 53, 52, 52,
        52, 51, 51, 50, 50, 50, 50, 50, 50, 50, 51, 51, 51, 51, 51, 52, 52, 52, 52, 52, 53, 53, 53, 53, 53, 54, 54, 54,
        54, 54, 55, 55, 55
    ];

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
        [-1, -2, -3, -2, -1, -1, -1, 0, -1, 0, -1, -1, 0, -1, 0, -1]
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

    static double TwentyOffset(int paren) => paren is < 52 or > 112 ? 0 : Twenty[paren - 52] - Seven[paren - 52];

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

    static double AtIndentRow(int row, int paren) => paren <= 53 ? 122 - paren : Nearest(Indents[row], paren, 53);

    /// <summary>
    ///     A row measured three columns apart from <paramref name="first" />, read at its nearest column.
    /// </summary>
    static double Nearest(int[] row, int paren, int first) {
        if (paren <= first) {
            return row[0];
        }

        var index = (paren - first + 1) / 3;
        return index >= row.Length ? row[^1] + (paren - (first + (row.Length - 1) * 3)) / 5.0 : row[index];
    }
}
