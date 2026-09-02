using System.Threading;

// ⚠ The `Acquire()`/`Release()` protocol escape, written across two parts of a partial type. The
// walk that finds the release starts from the *syntactic* type declaration holding the enter, so it
// sees one part and not the other — and a partial type's parts are usually in different files,
// where nothing in the enter's tree mentions the release at all. Declining a partial type outright
// is the price of keeping this rule `scope: Semantic`: the alternative is to walk every
// `DeclaringSyntaxReference`, which makes the answer for one file depend on files the cache key does
// not name, and that is the whole reason `SK3043` and `SK3044` cost what they cost.
partial class Pump {
    static readonly object gate = new object();

    public void Acquire() {
        Monitor.Enter(gate);
    }
}

partial class Pump {
    public void Release() {
        Monitor.Exit(gate);
    }
}
