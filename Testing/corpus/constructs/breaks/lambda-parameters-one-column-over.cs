// A local's lambda with a bare-name body on a line exactly one column past the margin (#572): the
// parameter list chops up to a head that the type and the body set, a body of eight only at a head of
// 49, and past that the #558 rules hold. One column further (122) nothing chops.
class C {
    void M() {
        Func<TTTT> fffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvv;
        Func<TTTT> fffffffffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvv;
        Func<TTTT> ffffffffffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvv;
        Func<TTTT> fffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvvvv;
        Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
        Func<TTTTTTTTTTTTTTTTTTTT> ffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvvvvvvv;
        Func<TTTTTTTTTTTTTTTTTTTT> ffffffffffffffffffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => v;
        Func<TTTT> fffffffff = (PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP p0) => vvv;
    }
}
