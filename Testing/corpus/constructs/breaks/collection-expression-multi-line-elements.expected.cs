// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A collection expression's fill around an element that spans lines (#471, SK-DIV-0117): the element
// keeps its head on the line before it — `1, () => {`, `1, o switch {`, `[1], [` — and the element after
// it starts a line of its own — `},` / `2, 3`, `],` / `[3]`, `+ 2,` / `3`. The array initializer's two
// rules, which a collection expression did not have.
//
// ⚠ Not `[null!, ((x,` / `y) => { })]`: the oracle keeps `null!, ((` as well, but nests the parameters
// from the grouping parenthesis's line, and that is SK-DIV-0118's.

class CollectionExpressionMultiLineElements {
    void M() {
        object[] b = [
            1, () => {
                A();
                B();
            },
            2, 3
        ];
        object[] c = [
            1, o switch {
                1 => 2,
                _ => 3
            },
            2
        ];
        object[] e = [
            [1], [
                2
            ],
            [3]
        ];
        int[] f = [
            1
            + 2,
            3
        ];
        object[] g = [
            1, 2, Call(
                alpha,
                beta
            ),
            3, 4
        ];
        var h = new object[] { 1, () => { A(); }, 2, 3 };
    }

    // An identifier head stays when the break inside the element is certain, the tuple's rule; an
    // element that is merely too wide moves whole, delimited or not; a kept break after it stays.
    void Heads() {
        object[] a = [
            1,
            ("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                2),
            3
        ];
        object[] b = [
            1,
            F(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                2
            ),
            3
        ];
        object[] c = [
            1, F(() => {
                    A();
                    B();
                }
            ),
            2
        ];
        object[] d = [
            1, () => {
                A();
                B();
            },
            2,
            3
        ];
        object[] e = [1, new C { X = 1, Y = 2 }, 2];
        object[] f = [
            1, [2, 3], [
                4
            ],
            5
        ];
        var t = (1, () => {
            A();
            B();
        }, 2, 3);
    }
}
