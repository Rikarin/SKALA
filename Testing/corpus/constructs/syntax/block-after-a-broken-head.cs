// An anonymous function's block and an object creation's initializer nest from the line the construct
// starts on, not from the line their `{` lands on — the switch expression's rule (SK-DIV-0107). The two
// differ when the parameter list, the arrow or the argument list broke under a continuation the
// statement opened on the construct's own line: `_f = delegate(` / `int first` / `) {` puts the
// statement at the `=`'s level plus one and `};` on it, where Skala nested from the `) {` line as though
// it were still inside the `=`'s continuation, and moved the oracle's own fixed point (issue #413,
// SK-DIV-0164). The input is what Skala wrote before; the oracle maps it back. A construct that starts
// on a line of its own — after an `=` the author broke, after `??`, in a ternary branch, as an
// argument, after an expression body's arrow — nests from that line, and a method's, a constructor's
// and a local function's block is unmoved by a broken parameter list or base call.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace P;

public class T {
    public T(int a, int b, int c) { }

    public int P { get; set; }

    public int Q { get; set; }
}

public class B {
    public B(int a, int b) { }
}

public class C : B {
    const int aVeryLongArgumentNameNumberOne = 1;
    const int aVeryLongArgumentNameNumberTwo = 2;
    const int aVeryLongArgumentNameNumberThree = 3;

    Func<int, int> _f = delegate(
        int first
    ) {
            return first;
        };

    Func<int, int, int> _g = (
        int first,
        int second
    ) => {
            return first;
        };

    T _t = new T(
        aVeryLongArgumentNameNumberOne,
        aVeryLongArgumentNameNumberTwo,
        aVeryLongArgumentNameNumberThree
    ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };

    List<int> _l;

    public C(int x)
        : base(
            x,
            2) { Use(x); }

    Func<int, int> Body =>
        delegate(
            int first
        ) {
            return first;
        };

    void N(
        int x) { Use(x); }

    void Assignments() {
        _f = delegate(
            int first
        ) {
                return first;
            };
        _g = (
            int first,
            int second
        ) => {
                return first;
            };
        _f = (
            int first
        ) => {
                return first;
            };
        _f = (int first)
            => {
                return first;
            };
        _f = first
            => {
                return first;
            };
        _f += delegate(
            int first
        ) {
                return first;
            };
        _l = new List<int>(
            aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo + aVeryLongArgumentNameNumberThree
        ) { aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
    }

    void Declarations() {
        Func<int, int> f = delegate(
            int first
        ) {
                return first;
            };
        var g = (
            int first,
            int second
        ) => {
                return first;
            };
        Func<int, Task<int>> a = async (
            int first
        ) => {
                return await Task.FromResult(first);
            };
        Func<int, int> s = static (
            int first
        ) => {
                return first;
            };
        T i = new(
            aVeryLongArgumentNameNumberOne,
            aVeryLongArgumentNameNumberTwo,
            aVeryLongArgumentNameNumberThree
        ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
        int L(
            int x) { return x; }

        L(1);
    }

    Func<int, int, int> Returned() {
        return (
            int first,
            int second
        ) => {
            return first;
        };
    }

    T Created() {
        return new T(
            aVeryLongArgumentNameNumberOne,
            aVeryLongArgumentNameNumberTwo,
            aVeryLongArgumentNameNumberThree
        ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
    }

    void OnTheirOwnLine(bool flag) {
        _f =
            delegate(
                int first
            ) {
                return first;
            };
        _f = _f
            ?? delegate(
                int first
            ) {
                return first;
            };
        _f = flag
            ? delegate(
                int first
            ) {
                return first;
            }
            : null;
        _t = _t ?? new T(
            aVeryLongArgumentNameNumberOne,
            aVeryLongArgumentNameNumberTwo,
            aVeryLongArgumentNameNumberThree
        ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
        Use(
            1,
            (
                int first,
                int second
            ) => {
                return first;
            }
        );
        Use(new T(
            aVeryLongArgumentNameNumberOne,
            aVeryLongArgumentNameNumberTwo,
            aVeryLongArgumentNameNumberThree
        ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo });
    }

    static void Use(int x) { }

    static void Use(int a, Func<int, int, int> f) { }

    static void Use(T t) { }

    public class D {
        Func<int, int> _f;
        T _t;

        void M() {
            _f = delegate(
                int first
            ) {
                    return first;
                };
            _f = (int first)
                => {
                    return first;
                };
            _t = new T(
                aVeryLongArgumentNameNumberOne,
                aVeryLongArgumentNameNumberTwo,
                aVeryLongArgumentNameNumberThree
            ) { P = aVeryLongArgumentNameNumberOne + aVeryLongArgumentNameNumberTwo, Q = aVeryLongArgumentNameNumberThree + aVeryLongArgumentNameNumberTwo };
        }
    }
}
