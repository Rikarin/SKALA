// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A delimited first argument behind a comment after the outer list's `(` (#521, SK-DIV-0165): the outer
// list's level counts although the inner construct opened on the same line — `Compute( /* f */ Inner(` /
// the inner arguments two levels in / `)` one / `);` — for a call, a lone argument or the first of two,
// and an array initializer alike. A first argument without a delimiter keeps the ordinary layout.

class C {
    void M() {
        var x1 = Compute( /* f */ Inner(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        );
        var x2 = Compute( /* f */ Inner(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            ),
            b
        );
        var x3 = Compute( /* f */ Inner(
                alpha,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        );
        var x4 = Compute( /* f */ new[] {
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            }
        );
        Compute( /* f */ Inner(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        );
        var x5 = Compute( /* f */ b,
            Inner(
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        );
        var x6 = Compute( /* f */ "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    }
}
