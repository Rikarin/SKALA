// #563: the root of a stepped conditional chain nests like a lone conditional, as an argument too.
class C563 {
    void A(int x, bool b) {
        M(
            x,
            x == 0
            ? 1
            : b ? 2 : 3
        );
        M(
            x,
            x == 0
            ? 1
            : 3
        );
        M(
            x,
            x == 0
                ? 1
                : 3
        );
        M(x == 0
            ? 1
            : b ? 2 : 3);
        var y = new[] {
            x == 0
            ? 1
            : b ? 2 : 3
        };
    }
}

class C563b {
    string P(int a) => a > 10 ? "ten" : a > 5 ? "five" : a > 1 ? "one" : "zero";

    string Q(int a) {
        return a > 10 ? "ten" : a > 5 ? "five" : "zero";
    }

    string R(int a) {
        var s = a > 10 ? "ten"
            : a > 5 ? "five"
            : "zero";
        return s;
    }

    void A(int newLines, bool e, object gap) {
        Break(
            newLines,
            gap,
            newLines == 0
            ? DefaultNewLine()
            : e ? DefaultNewLine() : FirstNewLine(gap) ?? DefaultNewLine()
        );
    }
}
