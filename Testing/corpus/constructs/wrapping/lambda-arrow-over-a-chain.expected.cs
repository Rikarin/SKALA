// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A lambda that is an `=`'s value, over a binary operand chain: the arrow wins (#453, SK-DIV-0050).
// The body moves below the arrow whenever it does not fit beside it, and the chain breaks only when it
// does not fit below either — never the `=`, never the chain first. In a field, a local, an assignment
// and a `??=`, from 100 to 170 columns. A chain or an arrow the author broke is kept.
//
// ⚠ Not a call body (`() => Call(…)`), whose preference against the argument list is the open half of
// SK-DIV-0050, and not a lambda argument: Serilog's `.Where(m => m.IsDefined(…)` / `&& …` keeps the
// arrow and breaks the chain.

class C {
    static readonly Func<int, string> F = value =>
        value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssss";

    static readonly Func<int, string> F = value =>
        value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";

    void M() {
        Func<int, string> f = value => value.ToString() + "sssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value => value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value => value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value =>
            value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value =>
            value.ToString() + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value =>
            value.ToString() + "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value =>
            value.ToString()
            + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> f = value =>
            value.ToString()
            + "sssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        _g = (first, second) =>
            first.Length + second.Length + "ssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        _g = (first, second) =>
            first.Length + second.Length + "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        f ??= value =>
            value > 0 && value < 1000 && "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" != null;
        f ??= value =>
            value > 0
            && value < 1000
            && "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss" != null;
        Func<int, string> h = value => value.ToString()
            + "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss";
        Func<int, string> k = value =>
            value.ToString() + "short";
    }
}
