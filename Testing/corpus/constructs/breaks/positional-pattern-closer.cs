// #559: a positional pattern's `)` on a line of its own sits on its elements' column.
class C559 {
    int A(object owner) =>
        owner switch {
            (
                int aaaaaaaaaaaaaaaaaaaaaaa,
                int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) =>
                1,
            _ => 0
        };

    int B(object owner) =>
        owner switch {
            (
                int aaaaaaaaaaaaaaaaaaaaaaa,
                int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ) => 1,
            _ => 0
        };

    int C(object owner) =>
        owner switch {
            Foo(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccc, ddd
            ) =>
                1,
            _ => 0
        };

    int D(object owner) =>
        owner switch {
            Foo(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccc, ddd
            ) => 1,
            _ => 0
        };

    int G(object owner) =>
        owner switch {
            (aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccc, dddddddddddddddddddd) =>
                1,
            _ => 0
        };

    int H(object owner) =>
        owner switch {
            (
                int aaaaaaaaaaaaaaaaaaaaaaa,
                int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            )
                => 1,
            _ => 0
        };

    void I(object owner) {
        switch (owner) {
            case (
                int aaaaaaaaaaaaaaaaaaaaaaa,
                int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
            ):
                break;
        }

        var x = owner is (
            int aaaaaaaaaaaaaaaaaaaaaaa,
            int bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
        );
    }
}
