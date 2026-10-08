// ⚠ An array initializer's first element joins a block comment on its own line above it: the `{`'s break
// is taken before the comment. Not after a comment on the brace's line, not after a `//`, and not in a
// collection, object or anonymous initializer or a collection expression, which keep the break. Issue #522.
class C {
    int[] f = {
        /* c */
        1
    };

    void M() {
        var a = new[] {
            /* c */
            1
        };
        var b = new[] {
            /* c */
            /* d */
            1, 2
        };
        var c = new int[] {
            /* c */ /* d */
            new[] { 1 }.Length
        };
        var d = new[] { /* c */
            1
        };
        var e = new[] {
            // c
            1
        };
        var g = new List<int> {
            /* c */
            1
        };
        var h = new P {
            /* c */
            X = 1
        };
        int[] i = [
            /* c */
            1
        ];
        var j = new {
            /* c */
            X = 1
        };
    }
}
