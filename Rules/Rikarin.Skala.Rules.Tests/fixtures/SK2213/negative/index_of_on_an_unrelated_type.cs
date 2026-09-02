// ⚠ A method called `IndexOf` on a type outside the covered contracts may return anything at all
// — this one returns a 1-based position, where `> 0` is exactly right. The same reasoning stops
// SK2053 trusting a hand-written `Count`.
class Ledger {
    public int IndexOf(string key) => key.Length;
}

class C {
    bool Present(Ledger ledger, string key) => ledger.IndexOf(key) > 0;
}
