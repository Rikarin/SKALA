// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// ⚠ A comment on a namespace's or a type's `{` line leaves the gap under it the brace's: the first member
// pays no `blank_lines_around_*` there, for `//` and `/* */`, before a one-line and a multi-line member,
// and an author's blank line under the comment is kept rather than removed as near the brace. Issue #499.

namespace N1 { /* b3 */
    class X { }
}

namespace N2 { // b3
    class X { }
}

namespace N3 { /* b3 */
    class X { }

    class Y { }
}

namespace N4 { /* b3 */

    class X { }
}

namespace N5 {
    class T { /* c */
        void M() {
            A();
        }
    }

    class U { // c
        void M() {
            A();
        }
    }

    class V { // c

        void M() { }
    }
}
