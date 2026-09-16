namespace Constructs.Breaks;

// SK-DIV-0107 (issue #370). A switch expression's arms take one level from the line its governing
// expression starts on, not from the line its `{` lands on. The two differ when the governing
// expression is multi-line under a continuation the statement opened but never wrote a break at: a
// tuple, a binary chain, a call chain or an invocation broken inside, after `var s =`, `int s =`,
// `s =` or `return`. Under an arrow the arrow's own level is written first and both readings agree.
// Before this file Skala nested the arms from the brace's line, one level too deep.
public class SwitchOverMultilineGoverningExpression {
    int Tuple(int a, int b) {
        var s = (a,
            b) switch {
            (1, _) => 1,
            _ => 2
        };
        return s;
    }

    int Binary(int a, int b) {
        var s = (a
            + b) switch {
            1 => 1,
            _ => 2
        };
        return s;
    }

    int Chain(int a) {
        var s = a.ToString()
            .Length switch {
            1 => 1,
            _ => 2
        };
        return s;
    }

    int Invocation(int a, int b) {
        var s = F(a,
            b) switch {
            1 => 1,
            _ => 2
        };
        return s;
    }

    int Typed(int a, int b) {
        int s = (a,
            b) switch {
            (1, _) => 1,
            _ => 2
        };
        return s;
    }

    int Assigned(int a, int b) {
        int s;
        s = (a,
            b) switch {
            (1, _) => 1,
            _ => 2
        };
        return s;
    }

    int Returned(int a, int b) {
        return (a
            + b) switch {
            1 => 1,
            _ => 2
        };
    }

    int AsAnArgument(int a, int b) {
        var s = F((a,
            b) switch {
            (1, _) => 1,
            _ => 2
        }, 3);
        return s;
    }

    int AsAnOperand(int a, int b) {
        var s = a + (a,
            b) switch {
            (1, _) => 1,
            _ => 2
        };
        return s;
    }

    int UnderAnArrow(int a, int b) => (a,
        b) switch {
        (1, _) => 1,
        _ => 2
    };

    int SingleLine(int a, int b) {
        var s = (a, b) switch {
            (1, _) => 1,
            _ => 2
        };
        return s;
    }

    int F(object a, int b) => b;
}
