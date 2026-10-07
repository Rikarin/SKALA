// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// Issue #426. A blank line inside a construct was kept wherever the break before it survived: between
// a trailing comment and the closer, between the items of a chopped list, before a chain's dot or a
// conditional's '?'. The oracle keeps one only before a statement, a member, an accessor, a switch
// section or arm, a collection-expression element, else/catch/finally or a comment, and after a line
// comment inside braces.

class K {
    void A() {
        M(1, 2);
        M(
            1,
            2 // e
        );
        M(
            1,
            2 /*e*/
        );
        M(
            1,
            2 /*e*/
        );
        var a = new[] {
            1, 2 // e
        };
        var b = new int[] { 1, 2 /*e*/ };
        var x = arr[1,
            2 // e
        ];
        var y = arr[1,
            2 /*e*/
        ];
        var l = new List<int> {
            1, 2 // e
        };
        Foo<int, string // e
        >();
        Foo<int,
            string /*e*/
        >();
        if (a) {
            M(); // e
        }

        if (a) {
            M(); /*e*/
        }
    }

    void B(
        int a,
        int b // e
    ) { }

    void C(
        int a,
        int b /*e*/
    ) { }

    [Attr(
        1,
        2 // e
    )]
    void D() { }

    int E =>
        F(
            1,
            2 // e
        );
}

class K2 {
    [Aaaaaaaaaaaaaaaaa, // e
     Bbbbbbbbbbbbbbbbbbbb]
    void A() {
        var ar = new int[] {
            1, // e

            2
        };
        var an = new {
            A = 1, // e

            B = 2
        };
        var ar4 = new int[] { 1, /* e */ 2 };
        if (o is {
                A: 1, // e

                B: 2
            }) { }

        if (o is {
                A: 1,
                // e

                B: 2
            }) { }

        var x = new D { // e

            A = 1, B = 2
        };
        Foo<int, // e
            string>();
        var y = a // e
            ? b
            : c;
    }
}

class K3 {
    int f1 = 1,
        f2 = 2;

    int P {
        get => 1;

        set { }
    }

    void A() {
        var o = new Ooooooooooooooooooooooooooooooooooo {
            Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
            Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
            Cccccccccccccccccccccc = 3333333333333
        };
        var an = new {
            Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
            Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
            Cccccccccccccccccccccc = 3333333333333
        };
        int[] ce = [
            111111111, 222222222, 333333333, 444444444, 111111111, 222222222, 333333333, 444444444,

            555555555, 666666666, 111111111, 222222222, 333333333
        ];
        var w = rrrrrrrrrrrrrrrrrrrrrrrrrrrrrr with {
            Aaaaaaaaaaaaaaaaaaaaaaaaa = 1111111111,
            Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb = 2222222222,
            Cccccccccccccccccccccc = 3333333333333
        };
        if (o is {
                Aaaaaaaaaaaaaaaaaaaaaaaaa: 1111111111, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb: 2222222222, Cccccccccccc: 3
            }) { }

        int a = 1,
            b = 2;
        for (int i = 0;
             i < 10;
             i++) { }

        var s = v switch {
            1 => 2, // c

            _ => 3
        };
        var s2 = v switch {
            1 => 2,
            // c

            _ => 3
        };
        switch (v) {
            case 1:

            case 2:
                break;
        }

        Bar(
            1,
            // c

            // d
            2
        );
    }

    void Lo(
        int a,
        // c
        int b
    ) { }
}

enum E3 {
    A,

    B
}

record R3(
    int A,
    int B);

class K4 {
    void A() {
        var x = aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            + cccccccccccccccccccccccccccccccccccccc;
        var y = source
            .Select(z => z)
            .Where(z => z);
        var s = v switch {
            1 => 2,

            _ => 3
        };
        var o = new Ooo { A = 1, B = 2 };
        var l = new List<int> {
            111111111,
            222222222,
            333333333,
            444444444,
            111111111,
            222222222,
            333333333,
            444444444,
            111111111,
            222222222,
            555555555,
            666666666
        };
        Foo(x => {
                M();

                N();
            }
        );
        Foo(
            x => {
                M();

                N();
            },
            2
        );
        var q = from a in b
            where a
            select a;
        var p = cond
            ? 1
            : 2;
        if (a && b) { }

        Bar(
            1,
            // own line
            2
        );
        Bar(
            1,

            // own line
            2
        );
    }

    [Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
     Bbbbbbbbbbbbbbbbbbbbbbbbb]
    void B<T, U>()
        where T : class
        where U : struct { }
}

class D4 : Aaaaaaaaaaaaaaaaaaaaa,
    Bbbbbbbbbbbbbbbbbbbbbb { }
