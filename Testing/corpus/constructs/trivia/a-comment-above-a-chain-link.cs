// ⚠ A comment on its own line above a chain link takes the link's column, and the link stays on it: one
// level in, with a blank line above the comment or without, for `//` and `/* */`, `.` and `?.`, after a
// declaration's `=` and inside an argument. Issue #523.
class C {
    void M() {
        var y = a

            // c
            .B();
        var z = a
            // c
            .B();
        var w = a
            /* c */
            .B();
        var v = a

            // c
            // d
            ?.B();
        var u = a
            .B()

            // c
            .C();
        Call(a

            // c
            .B());
        return a

            // c
            .B();
    }
}
