// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
using System;
using System.Collections.Generic;

namespace N;

// #410: the gap between a block comment and the token after it, each written closed and spaced.

public class G<T> where /*g01*/T : /*g02*/class {
    public int B() => 0;

    public void Q< /*g03*/U>() { }
}

public record Pt(int X /*g60*/, int Y /*g61*/);

[Obsolete("x" /*n01*/)]
public class C {
    int[] a = [1, 2];
    int f;
    bool b;

    public C() /*d18*/ : base() { }

    public C(int q) : this( /*d17*/) { }

    public void M(int x, int y) { }

    public void P(int p1 /*n02*/, int p2 /*n03*/, int /*g62*/p3) { }

    void Rf(ref int z) { }

    public int Pr { /*d41*/ get; set; }

    public int Ps { get /*d42*/; /*d43*/ set; }

    public /*d05*/ void A1() { }

    void /*d07*/ A3() { }

    public int Y() => /*c91*/f;

    public int Z() => /*c92*/ f;

    public void Closers() {
        M(1 /*c01*/, 2);
        M(1 /*c02*/, 2);
        M(1, 2 /*c03*/);
        M(1, 2 /*c04*/);
        var v1 = 1 /*c05*/;
        var v2 = 1 /*c06*/;
        var v3 = f /*c07*/.ToString();
        var v4 = f /*c08*/.ToString();
        var v5 = a[1 /*c09*/];
        var v6 = a[1 /*c10*/];
        var v7 = (f /*c11*/);
        var v8 = (f /*c12*/);
        var v9 = new G<int /*c13*/>();
        var v10 = new G<int /*c14*/>();
        M /*c41*/(1, 2);
        M /*c42*/(1, 2);
        var v11 = a /*c43*/[1];
        var v12 = a /*c44*/[1];
        var v13 = new G /*c45*/<int>();
        var v14 = new G /*c46*/<int>();
        var v15 = a? /*c49*/.Length;
        var v16 = a /*c50*/?.Length;
        var v17 = f /*c77*/++;
        M(x /*c80*/: 1, y: 2);
        M(x /*c81*/: 1, y: 2);
        var v18 = typeof(int /*c74*/);
        var v19 = nameof(f /*c75*/);
        var v20 = typeof /*d22*/(int);
        var v21 = default /*d38*/(int);
        int /*g26*/[] r1 = [1];
        int[] c3 = [ /*g19*/];
        int[] c4 = [ /*g20*/];
        var v22 = new int[] { /*g25*/ };
        M(1 /*e11*/ /*e12*/, 2);
        M(1 /*e15*/ /*e16*/, 2);
        if (f > 0 /*c65*/) { }

        if /*c67*/ (f > 0) { }

        while /*d72*/ (f > 9) { }

        for (var i = 0 /*c71*/; i < 1; i++) { }
    }

    public void Operators() {
        var v1 = f /*c23*/ + 1;
        var v2 = f /*c24*/ + 1;
        var v3 = f /*c27*/ == 1;
        var v4 = f > 0 /*c29*/ && f < 9;
        f /*c31*/ = 2;
        f /*d57*/ += 1;
        Func<int, int> l1 = x /*c34*/ => x;
        var v5 = f > 0 /*c37*/ ? 1 : 2;
        var v6 = f > 0 ? 1 /*c39*/ : 2;
        var v7 = new int[] { 1 /*c59*/ };
        var v8 = new int[] { 1 /*c60*/ };
        var v9 = f /*c84*/ is int;
        b = f is /*g41*/ > 1 and /*g44*/ < 9;
        var v10 = f switch {
            1 /*d14*/ => 2,
            _ => 3
        };
        var v11 = f switch /*g27*/ {
            _ => 1
        };
        var v12 = new List<int>() { 1 } /*g48*/.Count;
    }

    public void Operands(object o, int[] xs) {
        M( /*c15*/1, 2);
        M( /*c16*/ 1, 2);
        var v1 = a[ /*c17*/1];
        var v2 = a[ /*c18*/ 1];
        M(1, /*c19*/2);
        M(1, /*c20*/ 2);
        M(1, /*c21*/2);
        var v3 = f + /*c25*/1;
        var v4 = f + /*c26*/ 1;
        var v5 = f == /*c28*/1;
        var v6 = f > 0 && /*c30*/f < 9;
        f = /*c32*/2;
        f = /*c33*/ 2;
        Func<int, int> l2 = x => /*c35*/x;
        Func<int, int> l3 = x => /*c36*/ x;
        var v7 = f > 0 ? /*c38*/1 : 2;
        var v8 = f > 0 ? 1 : /*c40*/2;
        var v9 = f. /*c47*/ToString();
        var v10 = f. /*c48*/ ToString();
        var v11 = - /*c51*/f;
        var v12 = ! /*c54*/ b;
        var v13 = (int) /*c55*/f;
        var v14 = (int) /*c56*/ f;
        var v15 = ( /*c57*/int)f;
        var v16 = new int[] { /*c61*/1 };
        var v17 = new int[] { /*c62*/ 1 };
        var v18 = new /*c63*/ C();
        var v19 = new /*c64*/C();
        if ( /*c69*/f > 0) { }

        for (var i = 0; /*c72*/i < 1; i++) { }

        var v20 = f + /*g08*/(f);
        var v21 = f + /*g10*/-1;
        Func<int, int, int> l4 = /*g15*/(x, y) => x;
        var v22 = b ? /*g31*/-1 : /*g32*/(1);
        var v23 = a[^ /*d52*/1];
        foreach (var x in /*d09*/xs) { }

        foreach (var y /*h01*/in xs) { }

        foreach (var z /*h02*/ in xs) { }

        var v24 = o switch {
            int i /*h04*/when i > 0 => 1,
            _ => 2
        };
        var v25 = new List<int> { /*g46*/1 };
        M(1, /*e13*/ /*e14*/2);
        throw /*d74*/new Exception();
    }

    public void Spaced(object o) {
        var v1 = f is /*c82*/ int;
        var v2 = f as /*c85*/ object;
        var v3 = o is /*d28*/ not null;
        M(x: /*c78*/ 1, y: 2);
        Rf(ref /*d26*/ f);
        int[] c1 = /*g17*/ [1];
        var v4 = o is int /*d46*/ i;
        switch (f) {
            case 3: /*d68*/ break;
        }
    }

    public int Returns() {
        return /*c87*/1;
    }
}

public class D /*c93*/ : object { }

public class F : /*c95*/object { }

public class L<T /*c100*/, U /*c101*/> where T /*c102*/ : class { }
