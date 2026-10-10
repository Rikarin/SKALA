// #559, SK-DIV-0443: a positional pattern heading an arm with a short body breaks after the arrow, then before
// it, then before the pattern's `)`, then inside; and an `is` over one before a `;` breaks before its `)` when
// only `);` overflows.
class C559h {
    object A1(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A2(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A3(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A4(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A5(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A6(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A7(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A8(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A9(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccccccccc) => 1,
            _ => null
        };

    object A10(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccc) => null,
            _ => null
        };

    object A11(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccc) => null,
            _ => null
        };

    object A12(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccccc) => null,
            _ => null
        };

    object A13(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccccccccccccc) => null,
            _ => null
        };

    object A14(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccccccccccccc) => null,
            _ => null
        };

    object A15(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF { X: 1 }, int bbbbbbbbbbbbbbbbbbbb) => 1,
            _ => null
        };

    object A16(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF { X: 1 }, int bbbbbbbbbbbbbbbbbbbb) => 1,
            _ => null
        };

    object A17(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF { X: 1 }, int bbbbbbbbbbbbbbbbbbbb) => 1,
            _ => null
        };

    object A18(object s) =>
        s switch {
            (int aaaaaaaaaaaaaaaaaaaaaaa, FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF { X: 1 }, int bbbbbbbbbbbbbbbbbbbb) => 1,
            _ => null
        };

    object A19(object s) =>
        s switch {
            (1, "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssssss", 2) => "sssssssss",
            _ => null
        };

    bool B20(object owner, bool x) {
        var x = owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccc, int dddd);
        return x;
    }

    bool B21(object owner, bool x) {
        bool x = owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccc, int dddd);
        return x;
    }

    bool B22(object owner, bool x) {
        x = owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccc, int dddd);
        return x;
    }

    bool B23(object owner, bool x) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccc, int dddd);
    }

    bool B24(object owner, bool x) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccc, int dddd);
    }
}
