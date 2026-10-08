// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// An `=` before a lambda with a bare name for a body (#453, SK-DIV-0050): while the line through the
// lambda's `=>` fits, the oracle breaks after the arrow and never after the `=` — with `()` and with a
// parameter list, under heads of 12 to 70 columns.

class LambdaArrowOverAName {
    void M() {
        Func<TT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = () =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaaaa a1, Bbbbbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) => Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT> f = (Aaaaa a1, Bbbbb b1) =>
            Qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqq;
    }
}
