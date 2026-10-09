// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-09
// #559, SK-DIV-0442: a local's `=` before `operand is (…)` breaks whenever the line overflows; an assignment's
// and a `return` keep the head and fill the pattern.

class C559e {
    bool M1(object owner, bool x) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccc, int dddd);
        return x;
    }

    bool M2(object owner, bool x) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc, int dddd);
        return x;
    }

    bool M3(object owner, bool x) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccc, int
                dddd);
        return x;
    }

    bool M4(object owner, bool x) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccc, int
                dddd);
        return x;
    }

    bool M5(object owner, bool x) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
                ccccccccccccccccccccccccccccccccccccccccccccccc, int dddd);
        return x;
    }

    bool M6(object owner, bool x) {
        bool x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccc, int dddd);
        return x;
    }

    bool M7(object owner, bool x) {
        bool x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccc, int dddd);
        return x;
    }

    bool M8(object owner, bool x) {
        bool x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccc, int
                dddd);
        return x;
    }

    bool M9(object owner, bool x) {
        bool x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
                cccccccccccccccccccccccccccccccccccc, int dddd);
        return x;
    }

    bool M10(object owner, bool x) {
        x = owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccc, int
            dddd);
        return x;
    }

    bool M11(object owner, bool x) {
        x = owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
            ccccccccccccccccccccccccccccccccccccc, int dddd);
        return x;
    }

    bool M12(object owner, bool x) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccc,
            int dddd);
    }

    bool M13(object owner, bool x) {
        var someLongerVariableName =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccc, int dddd);
        return x;
    }

    bool M14(object owner, bool x) {
        var someLongerVariableName =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccc, int dddd);
        return x;
    }

    bool M15(object owner, bool x) {
        var someLongerVariableName =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccc, int
                dddd);
        return x;
    }

    bool M16(object owner, bool x) {
        var someLongerVariableName =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
                cccccccccccccccccccccccccccccccccccc, int dddd);
        return x;
    }
}
