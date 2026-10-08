// #548: a conditional chain the author broke before any `?` is chopped at both signs of every member.
// Broken only at a `:` it does not step, and a conditional on the true side is not a chain member.
class TernaryChainSteppedByTheAuthor {
    int A(bool a, bool b) {
        return a
            ? 1
            : b ? 2 : 3;
    }

    int B(bool a, bool b) {
        return a ? 1
            : b ? 2 : 3;
    }

    int D(bool a, bool b, bool c) {
        return a
            ? 1
            : b ? 2 : c ? 3 : 4;
    }

    int E(bool a, bool b) {
        return a ? 1 : b
            ? 2 : 3;
    }

    int F(bool a, bool b, int k) => k switch {
        1 => a
            ? 1
            : b ? 2 : 3,
        _ => a ? 1 : b ? 2 : 3
    };

    int G(bool a, bool b) {
        return a
            ? b ? 2 : 3
            : 1;
    }

    int H(bool a, bool b) {
        var x = a
            ? 1 : b ? 2 : 3;
        return x;
    }

    int I(bool a, bool b) {
        return a ? 1 : b ? 2
            : 3;
    }

    string J(bool someLongConditionName, int k) => k switch {
        1 => someLongConditionName ? "a fairly long string literal value here" : "another fairly long string literal",
        _ => ""
    };
}
