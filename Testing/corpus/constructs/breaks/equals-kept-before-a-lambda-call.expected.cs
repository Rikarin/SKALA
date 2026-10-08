// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A local's `=` before a lambda whose body is a call, under a declarator name of nine columns or fewer
// (#453): the oracle never breaks the `=`, at any type width, body `(` column or line end measured, and
// the arguments chop instead. A wider name breaks it by rules that move with the name, the type and the
// `(` separately, which are not wired (SK-DIV-0050).

class C {
    void M() {
        Func<TTTTTTTTTT> f = () => Cccc(
            xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx,
            yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
        );
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> handler = () => Cccc(
            xxxxxxxxxxxxxxxxxxxxxxxxxxxxx,
            yyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
        );
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTT> f = () => CCCCCCCCCCCCCCCCCCCCCCCCCCCC(
            xxxxxxxxxxxxxxxxxx,
            yyyyyyyyyyyyyyyyyyy
        );
    }
}
