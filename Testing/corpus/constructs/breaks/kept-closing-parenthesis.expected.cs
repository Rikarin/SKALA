// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// Issue #442. A ')' the author put on a line of its own lands by three rules: a statement header's under
// its '(' when conditions are aligned, a typeof/sizeof/default/checked one on its opener's line, a grouping
// parenthesis's or a tuple's on a continuation line. Skala had all three wrong.

class K {
    void A() {
        var t = (1, 2
            );
        var t3 = (1, 2 // e
            );
        var u = typeof(int
        );
        var s = sizeof(int
        );
        var d = default(int
        );
        var c = checked(a + b
        );
        var p = (a + b
            );
        var q = (int
            )x;
        lock (o
             ) { }

        if (a
           ) { }

        while (a
              ) { }

        using (o
              ) { }

        foreach (var x in y
                ) { }

        for (int i = 0;
             i < 1;
             i++
            ) { }

        switch (a
               ) { }

        fixed (int* p = a
              ) { }

        do { } while (a
                     );

        if (a && b
           ) { }
    }
}

class K2 {
    void A() {
        p = (a + b
            );
        M(
            (a + b
            ),
            c
        );
        M(
            c,
            (a + b
            )
        );
        M(
            c,
            (a + b
            )
        );
        x.Y(
            (1, 2
            )
        );
        Foo(
            (a
            ).B
        );
        var t = ((1, 2
            ), 3);
        M(
            typeof(int
            )
        );
        M(
            c,
            typeof(int
            )
        );
        var z = typeof(int
        ).Name;
        var w = 1
            + typeof(int
            ).Name.Length;

        int F() =>
            (1
            );
    }

    (int, int) T = (1, 2
        );

    Type U = typeof(int
    );
}

namespace N {
    class K3 {
        void A() {
            var u = unchecked(a + b
            );
            var v = typeof(
                int);
            var w =
                typeof(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb>);
            var c = checked(aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb * ccccccccccccccccccccccccc);
            var d = checked(a + b);
            M(
                default(int
                ),
                x
            );
            if (o
               ) { } else if (p
                             ) { }

            if (Call(
                    a,
                    b
                )) { }

            if (a
                || (b
                )) { }

            while (Call(a)
                  ) { }

            try { } catch (Exception e) when (e
                                             ) { }

            var p = (a + b
                );
            var t = (aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccc);
        }
    }
}
