// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    int _a;

    // about M
    void M() { }

// stuck to the left margin, and the key's real subject
    void N() {
// inside a body, at column zero
        M();
        // indented with its owner
        M();
        // indented to neither
        M();
/* a block comment at column zero */
        M();
    }
}
