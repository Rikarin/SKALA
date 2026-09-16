namespace Constructs.Breaks;

// SK-DIV-0110 (issue #370). A tuple fills, and a fill breaks before an item only when that makes the
// item fit whole: a nested item that carries a kept break of its own has no flat form anywhere, so
// its head stays on the line — `, (2` — and the item breaks inside. Before this file the fill read
// such an item as infinitely wide and broke before it, leaving the comma alone on a line.
public class NestedMultilineTupleItem {
    void M() {
        var t = (1
            , (2
                , 3));
        var u = (1,
            (2,
                3));
        var v = (1
            , (2, 3));
        var w = ((1
            , 2), 3);
        var x = (1,
            (2, 3
                , 4));
        var a = (1,
            (2
                , 3));
        var c = (1
            , new T { X = 1
                , Y = 2 });
        var d = (1
            , (2
                , 3)
            , (4
                , 5));
        var e = (1
            , G<int
                , int>());
        var f = (1
            , (2
                , 3), 4);
    }

    int G<T1, T2>() => 0;

    int X, Y;
}
