// ⚠ A comment run with a blank line under it is not the member below's: it hangs from the member above,
// nothing goes between that member and the comment, and the member below's requirement is paid under the
// comment rather than above it. For `//` and `/* */`, one comment line or two, before a field, a method, an
// attributed member. A run glued to the member below is still that member's (SK-DIV-0172),
// and `blank_lines_after_using_list` stays above the comment. Issue #494.
using System;
// ReSharper disable All

namespace N;

class C {
    int _a;
    // own

    void M() {
        A();
    }
    // after M

    int _b;
    // one
    // two

    void N() {
        A();
    }

    int _c;
    /* own */

    void O() {
        A();
    }

    int _d;
    // own

    [Obsolete]
    int _e;
    // own

    [Obsolete] int _f;
    // one

    // two
    void P() {
        A();
    }

    int _g;
    // own

    int _h;
}
