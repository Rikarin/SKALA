// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class C {
    void M(bool b) {
        if (b) {
            M(b);
        }


        M(b);
    }

    // ⚠ The option unit was one `if`, and one `if` cannot see this key's rule — only its value. Two
    // more shapes that move with the key and that the old brace-shaped test could not see at all:
    // both are statements *with* a child block, and both end in a `;` rather than a `}`.
    void DoWhile(int f) {
        do {
            f--;
        } while (f > 0);


        M(f > 0);
    }

    void IfBracedElseBraceless(int f) {
        if (f > 0) {
            M(true);
        } else
            M(false);


        M(f > 0);
    }
}
