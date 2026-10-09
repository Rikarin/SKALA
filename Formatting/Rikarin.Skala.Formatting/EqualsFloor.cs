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
    ///     Whether an <c>=</c> before a call with two or more arguments may break at all: the name it assigns,
    ///     <paramref name="name" /> columns wide, with the callee, <paramref name="callee" /> wide, has to reach
    ///     far enough for the call's <c>(</c> at <paramref name="paren" /> (1-based) — <c>2·(name + callee) + paren ≥ 163</c>
    ///     (#589, SK-DIV-0400).
    /// </summary>
    /// <remarks>
    ///     ⚠ The new measurement is the split of one head between the type and the name. With the <c>(</c> fixed and
    ///     the arguments fixed, moving columns from the name into a local's type turns the oracle's answer from
    ///     <c>=</c> / call to <c>= Call(</c> / arguments chopped — at the <c>(</c> on 82 a type of 28 breaks and one of
    ///     31 chops, on 106 a type of 64 breaks and one of 67 chops. The tables below were measured under
    ///     <c>var</c>, whose three-column type leaves the name the whole head, and every typed local with a short
    ///     name and a long type got their <c>var</c> answer: <c>Tyyy… v148 = Select(a, b, x);</c> with the <c>(</c>
    ///     at 108 to 120 broke the <c>=</c> where the oracle chops (#589).
    ///     <para>
    ///         Measured 2026-10-09 with <c>Testing ask</c> on 10 717 cells of two or more arguments: typed locals with
    ///         types of 1 to 85 columns, <c>var</c> and <c>Tyy</c> (identical to the column), assignments, names of 2 to
    ///         68 columns, callees of 4, 7, 15 and 30, the <c>(</c> at 50 to 120, indents 8 and 20. The tables alone
    ///         agree on 8 397; the tables behind this gate on 10 476. The gate was fitted over integer weights on the
    ///         name, the <c>=</c>'s column, the indent and the callee; <c>name + callee</c> against the <c>(</c> is the
    ///         best of them and no weight on the indent improved it. The 241 cells it misses are a column or two off
    ///         the tables' own floor, mostly behind a callee of 30.
    ///     </para>
    /// </remarks>
    public static bool NameReachesTheCall(int name, int callee, int paren) => 2 * (name + callee) + paren >= 163;

    /// <summary>
    ///     For <c>v = X || Y</c> whose <c>X</c> does not fit beside the <c>=</c>: the head, statement start through the
    ///     <c>=</c>, from which the <c>=</c> breaks; <see cref="int.MaxValue" /> when it never does (#579, SK-DIV-0403).
    /// </summary>
    /// <param name="left">The width of <c>X</c>.</param>
    /// <param name="right">The width of <c>Y</c>.</param>
    /// <param name="pattern">Whether <c>X</c> is an <c>is</c> pattern; otherwise it is an <c>&amp;&amp;</c> chain.</param>
    /// <remarks>
    ///     ⚠ Measured 2026-10-09 with <c>Testing ask</c> on 5 160 cells: typed locals, <c>var</c> locals and assignments
    ///     (one answer to the column for all three, so the head and not the name decides here), heads of 7 to 16,
    ///     <c>&amp;&amp;</c> chains of 90 to 129 columns and patterns of 100 to 132, <c>Y</c> of 1 to 100. The twelve-column
    ///     head of #379 and #553 is the floor while <c>Y</c> is short, and a wide <c>Y</c> lowers it — the "the floor
    ///     moves with the far operand" the first measurement recorded, now a table: 11 from a <c>Y</c> of 60, 10 from 68,
    ///     9 from 73, 8 from 92 and 7 from 93 behind a chain; a pattern holds each step a few columns longer. A pattern
    ///     too wide for the line below never breaks the <c>=</c> while <c>Y</c> is at most its own width less 108 — the
    ///     "keeps it for a wider pattern" of the first measurement, which was a <c>Y</c> of four.
    /// </remarks>
    public static int OrHeadFloor(int left, int right, bool pattern) {
        if (pattern) {
            return right <= left - 108 ? int.MaxValue
                : right >= 92 ? 8
                : right >= 76 ? 10
                : right >= 65 ? 11
                : 12;
        }

        return right >= 93 ? 7
            : right >= 92 ? 8
            : right >= 73 ? 9
            : right >= 68 ? 10
            : right >= 60 ? 11
            : 12;
    }

    /// <summary>
    ///     Whether an <c>=</c> whose line does not fit breaks and moves its value down whole, rather than staying and
    ///     letting the value break after a fragment of it: a plain member value's dot fill (<c>T name = receiver.A</c> /
    ///     <c>.B;</c>, #590, SK-DIV-0401) and a conditional's <c>?</c> / <c>:</c> when the condition fits beside the
    ///     <c>=</c> (#577, SK-DIV-0402).
    /// </summary>
    /// <param name="equals">The <c>=</c>'s column, 1-based.</param>
    /// <param name="name">The width of the name the <c>=</c> assigns.</param>
    /// <param name="fragment">
    ///     What would stay beside the <c>=</c>: the receiver and every link after it that fits there, or the condition.
    /// </param>
    /// <param name="below">The column the value would end at on the line below, its <c>;</c> included, 1-based.</param>
    /// <remarks>
    ///     ⚠ The new measurement is the name. The tables before this were measured under <c>var</c> heads, where the name
    ///     is the whole head and grows with the <c>=</c>'s column, so what read as "the head" or "the column of the
    ///     <c>=</c>" was the name: under a typed local whose type takes the head, the <c>=</c>'s column barely matters
    ///     and the name decides. Two questions, and the name is in both:
    ///     <list type="bullet">
    ///         <item>
    ///             Is the fragment short against the name? A name of eight or more always breaks the <c>=</c>, six or
    ///             seven up to a fragment of 51; a shorter one breaks it for a fragment up to <c>3·name + 7</c>, further
    ///             once the <c>=</c> passes column 84 — to 15 for a one-column name, 20 for two, any for three or more.
    ///         </item>
    ///         <item>
    ///             Does the value fit below by the measured limit? About 111 for a short fragment, a column less for every
    ///             two or three of fragment (never under 99), a column more for names of 8, 12, 16 and 20, none at all for
    ///             a fragment within a column of a quarter of the name; for names past 26 a column less per four of
    ///             name; and past an <c>=</c> at column 84 a column more per two.
    ///         </item>
    ///     </list>
    ///     Measured 2026-10-09 with <c>Testing ask</c> on member values, 52 599 cells — typed locals, <c>var</c> locals
    ///     and assignments, names of 1 to 100, receivers of 3 to 40, links of 1 to 40, two to four links, the <c>=</c>
    ///     at columns 14 to 115, values of 40 to 107, indents 8 and 16: this agrees on 51 792, keeping the <c>=</c>
    ///     unless the receiver overflows agreed on 29 150. A conditional takes the fragment question and, under a name
    ///     shorter than six, the limit (see <c>Fitter</c>'s conditional rule). ⚠ Most of what is left is a column
    ///     either side of the limit.
    /// </remarks>
    public static bool BreaksBeforeTheValue(int equals, int name, int fragment, int below) =>
        FitsBelow(equals, name, fragment, below) && FragmentIsShort(equals, name, fragment);

    /// <summary><see cref="BreaksBeforeTheValue" />'s first question: does the value fit below by the measured limit.</summary>
    public static bool FitsBelow(int equals, int name, int fragment, int below) =>
        below <= ValueLimit(equals, name, fragment);

    /// <summary><see cref="BreaksBeforeTheValue" />'s second question: is the fragment short against the name.</summary>
    public static bool FragmentIsShort(int equals, int name, int fragment) {
        if (name >= 6) {
            return name >= 8 || fragment <= 51;
        }

        var floor = 3 * name + 7;
        var bump = equals switch {
            < 84 => 0,
            84 => 1,
            85 => 3,
            86 => 4,
            87 => 5,
            88 => 7,
            _ => 999
        };

        var cap = name switch {
            1 => 15,
            2 => 20,
            _ => 999
        };

        return fragment <= Math.Min(floor + bump, Math.Max(floor, cap));
    }

    static int ValueLimit(int equals, int name, int fragment) {
        if (name < 8 && fragment <= 5 && equals >= 88) {
            return int.MaxValue;
        }

        var small = name >= 8 && 4 * fragment <= name + 4 ? int.MaxValue / 2
            : fragment > 11 ? (int)Math.Floor(Math.Max(99, 109 - 0.3 * (fragment - 11))) + (name >= 16 ? 1 : 0)
            : name < 8 ? fragment <= 5 ? 111 : fragment <= 8 ? 110 : 109
            : fragment <= 5 ? 112
            : 109 + (name >= 12 ? 1 : 0) + (name >= 20 ? 1 : 0);
        var steep = 119 - Math.Max(0, Math.Min(fragment, 11) - 5) / 2.0 - Math.Max(0, fragment - 11) / 3.0;
        var large = name >= 26 ? (int)Math.Floor(steep - name / 4.0) : int.MaxValue / 2;
        var bump = Math.Max(0, equals - 83) / 2;
        if (name < 8) {
            bump = Math.Min(bump, fragment <= 8 ? 2 : fragment <= 13 ? 1 : 0);
        }

        return Math.Min(small, large) + bump;
    }

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
    public static bool BreaksTheOperandArrow(int arrow, int parameters, int first, int end) {
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
