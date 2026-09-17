namespace Constructs.Breaks;

// SK-DIV-0109 (issue #370), and the binary half of SK-DIV-0007. A list item made multi-line by a
// break the author kept inside it — before a binary operator, inside a parenthesis, in a lambda's
// body — chops the list around it, exactly as an item too wide would: "chop if long *or
// multi-line*". The same containment reaches every container: an outer binary operator around a
// broken inner one chops too, a ternary around a broken branch chops, an object initializer around
// a broken element breaks its braces. A tuple, which only fills, keeps the author's arrangement.
// Before this file only a nested *delimited* list or a kept `=` break made its container chop.
public class MultilineItemChopsTheList {
    void Parameter(int a = 5
        + 6) { }

    void SecondParameter(int a = 5, int b = 5
        + 6) { }

    void FirstOfTwo(int a = 5
        + 6, int b = 7) { }

    void Arguments(bool c) {
        F(1
            + 2, 3);
        F(1, 2
            + 3);
        G(1
            + 2);
        F((1
            + 2), 3);
        F(x => x
            + 1, 3);
        F(1
            + 2
            + 3, 4);
        F(c && a > 0
            || b > 0, 3);
    }

    void OtherContainers(bool c, int n) {
        var o = new T { X = 1
            + 2, Y = 2 };
        var t = (1
            + 2, 3);
        var z = a > 0
            && b > 0 || c;
        var q = a > 0 && b > 0
            || c;
        var r = c ? 1
            + n : 2;
        if (c
            || n > 0
            || a > 0 && b > 0) {
            return;
        }
    }

    [System.Obsolete("x"
        + "y", true)]
    void Attributed() { }

    void F(object a, int b) { }

    void G(int a) { }

    int X, Y, a, b;
}

public class T {
    public int X, Y;
}
