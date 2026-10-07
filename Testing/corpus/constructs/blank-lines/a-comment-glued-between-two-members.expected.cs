// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// ⚠ A plain comment on the line under a member and directly above the next one makes the oracle count
// both as multi-line for the gap above them, and neither for the gap under them: a blank above `_a`,
// one above the comment, none under `_b`. Not when a blank separates the comment from either side, not
// for a comment on the member's own line, and not for a `///` run (pinned in
// SingleLineIsAnOutputFactIssue414Tests, so this file has no xmldoc row). Before a type's `}` as before
// a member. Issue #414, SK-DIV-0172.

class C {
    int _x;

    int _a;

    // one
    // two
    int _b;
    int _d;
    int _e; // on its own line
    int _f;
    int _g;
    // a blank under it

    int _h;
    int _i;

    // a blank above it
    int _j;

    int _k;

    /* a block comment */
    int _l;
    int _m;
    int _n;
    int _o;

    void X() { }

    void A() { }

    // between methods
    void B() { }
    void D() { }

    void M() {
        int L() => 1;

        // between local functions
        int K() => 2;
        int J() => L() + K();
    }

    int _p;

    int _q;
    // before the brace
}
