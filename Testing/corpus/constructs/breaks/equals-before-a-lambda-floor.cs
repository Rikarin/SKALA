// An `=` before a lambda with a bare-name body, past the margin (#558). While the line through `=>`
// fits, the arrow breaks for a value at least as wide as a floor that falls with the head, and a narrower
// value moves below the `=` whole. Once `(…) =>` itself overflows, the `=` breaks while the `)` is still
// on the line or the value is narrow enough for its body, and otherwise the parameter list chops.
class C {
    void M() {
        Func<A, B> ffffffffffffffffffffffffffff = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => vvvvvvvvvvvv;
        Func<A, B> fffffffffffffffffffffffffffffffffffff = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => vvvvvvvvvvvv;
        Func<A, B> ffffffffffffffffffffffffffffffffffffffffffffffff = (A a1, A a2, A a3, A a4, A a5, A a6, A a7, A a8, A a9x) => Name;
        Func<A, B> ffffffffffffffffffffffffffffffffffffffffffffffff = (A a1, A a2, A a3, A a4, A a5, A a6, A a7, A a8, A a9, A a10x) => vvvvvvvvvvvvvvvvvvvv;
        Func<A, B> f = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => value;
        Func<A, B> f = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => value;
        Func<A, B> fffffffffffffffffffffffffffffffffffffffffff = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv;
        Func<A, B> fffffffffffffffffffffff = (TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT p0) => vvvvvvvvvvvvvvvvvvvvvvvvvvvvvv;
    }
}
