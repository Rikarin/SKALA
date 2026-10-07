// An author's break before a subpattern's or a named argument's colon (#436). The oracle keeps it, puts
// the colon on its name's column, and lays the construct around it out as multi-line: a property
// pattern expands (plain, extended, nested, in a switch arm and in a case label) and a call's
// arguments chop, while a tuple and a positional pattern keep the rest of the list on the line.
// Skala joined every one. A break after the colon is the subpattern's own point and is the control.
public class SubpatternColonBreak {
    public int X;
    public int Y;
    public SubpatternColonBreak Q;

    void M(int a, int b) { }

    void T(object o) {
        var p1 = o is SubpatternColonBreak { X
: 1 };
        var p2 = o is SubpatternColonBreak { X
            : 1, Y: 2 };
        var p3 = o is SubpatternColonBreak { X: 1, Y
            : 2 };
        var p4 = o is SubpatternColonBreak { Q.X
            : 1 };
        var p5 = o is SubpatternColonBreak { Q: { X
            : 1 } };
        var p6 = o is SubpatternColonBreak { X:
            1 };
        var r1 = o is SubpatternColonBreak (A
            : 1, B: 2);
        var r2 = (a
            : 1, b: 2);
        M(a
            : 1, b: 2);
        var s = o switch {
            SubpatternColonBreak { X
                : 1 } => 1,
            _ => 0
        };
        switch (o) {
            case SubpatternColonBreak { X
                : 1 }:
                break;
        }
    }
}
