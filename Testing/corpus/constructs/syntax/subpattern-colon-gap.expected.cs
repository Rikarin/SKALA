// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// The gap in front of a subpattern's colon is ungoverned (#419): `{ X: 1 }` and `{ X : 1 }` both come
// back as written, a run collapses to one space, and one space always follows — no colon key moves
// either side, including the attribute colon keys that govern a named argument's colon. Skala answered
// it with the named-argument rule and removed the author's space. Every shape is written closed,
// spaced and as a two-space run, in a property pattern, an extended property pattern, a nested one and
// a positional one with and without its type. The named arguments, tuple names, constructor
// initializers and expression braces beside them are the governed controls, written both ways.

using System.Collections.Generic;

public class Base {
    public Base() { }
    public Base(int a) { }
}

public class Derived : Base {
    public Derived() : base() { }
    public Derived(int a) : base(a) { }
    public Derived(string s) : this() { }
}

public record Pair(int A, int B);

public class Shape {
    public int X;
    public Shape Q;
    public List<int> L = new List<int>();
}

public class SubpatternColonGap {
    void M(int a, int b) { }

    void Patterns(object o, Pair r) {
        var p1 = o is Shape { X: 1 };
        var p2 = o is Shape { X : 1 };
        var p3 = o is Shape { X : 1 };
        var p4 = o is Shape { X: 1 };
        var p5 = o is Shape { X : 1 };
        var e1 = o is Shape { Q.X: 1 };
        var e2 = o is Shape { Q.X : 1 };
        var n1 = o is Shape { Q: { X : 1 } };
        var r1 = r is (A: 1, B: _);
        var r2 = r is (A : 1, B : _);
        var r3 = r is Pair(A: 1, B: _);
        var r4 = r is Pair(A : 1, B : _);
    }

    void Controls(Pair r) {
        M(a: 1, b: 2);
        M(a: 1, b: 2);
        var t1 = (a: 1, b: 2);
        var t2 = (a: 1, b: 2);
        var i1 = new Shape { X = 1 };
        var i2 = new Shape { Q = new Shape { X = 1 } };
        var i3 = new Shape { L = { 1 } };
        var i4 = new Dictionary<int, int> { [1] = 2 };
        var i5 = new Dictionary<int, int> { { 1, 2 } };
        var i6 = new { X = 1 };
        var i7 = r with { A = 2 };
        var i8 = new List<(int, int)> { (1, 2) };
    }
}
