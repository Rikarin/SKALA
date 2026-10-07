using System;

// ⚠ Whether a member is single-line is a fact about the output, and every member below is written over
// several lines and joined by the formatter — an expression body by keep_existing_expr_member_arrangement,
// a field's initializer by place_simple_initializer_on_single_line, an empty body by
// skala_empty_block_style. Read off the source, each took blank_lines_around_* instead of
// blank_lines_around_single_line_* (issue #414). The last method is the other direction: one source
// line that the fitter breaks, which is multi-line.
class C {
    int _a = 1;
    int[] _b = new[] {
        1, 2, 3
    };
    int _c = 3;
    int A() => 1;
    int B() =>
        2;
    int D() => 3;
    public C() {
    }
    int E() => 4;

    void M() {
        int L() => 1;
        int K() =>
            2;
        int J() => 3;
        Console.WriteLine(L() + K() + J());
    }

    int F() => 5;
    int G() => System.Math.Max(111111111111111111, 222222222222222222) + System.Math.Max(333333333333333, 4444444444444444);
    int H() => 6;
}
