// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    void M() {
    #pragma warning disable CA1822
        M();
    #pragma warning restore CA1822
    }

    // ⚠ At the code's own indent rather than at column 0, so that `do_not_change` and `no_indent`
    // are told apart. See the `_if` fixture beside this one for the same note.
    void N() {
    #pragma warning disable CA1822
        N();
    #pragma warning restore CA1822
    }
}
