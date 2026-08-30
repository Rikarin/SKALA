// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class C {
    void M() {
        #if DEBUG
        M();
        #endif
    }

    // ⚠ Written at the code's own indent rather than at column 0, which is what separates
    // `do_not_change` from `no_indent`. With every directive in the file already at column 0 the two
    // values return the same bytes and the fixture cannot see the difference.
    void N() {
        #if DEBUG
        N();
        #endif
    }
}
