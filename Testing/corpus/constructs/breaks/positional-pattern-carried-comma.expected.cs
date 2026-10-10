// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-10
// #559, SK-DIV-0448: an element of a positional pattern or a deconstruction ending exactly at the margin carries
// its comma to the next line; a column either side the fill breaks after a comma as usual.

class C559c {
    object M1(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccccc,
                int dddddddddddddddd);
        return null;
    }

    object M2(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccccccc
                , int dddddddddddddddd);
        return null;
    }

    object M3(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
                cccccccccccccccccccccccccccccccccc, int dddddddddddddddd);
        return null;
    }

    object M4(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccccccccc,
                int dddddddddddddddd);
        return null;
    }

    object M5(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccccccc
                , int dddddddddddddddd);
        return null;
    }

    object M6(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb,
                cccccccccccccccccccccccccccccccccccccc, int dddddddddddddddd);
        return null;
    }

    object M7(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(),
                int dddddddddddddddd);
        return null;
    }

    object M8(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC()
                , int dddddddddddddddd);
        return null;
    }

    object M9(object owner) {
        var x =
            owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb,
                CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(), int dddddddddddddddd);
        return null;
    }

    object M10(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int ccccccccccccccccccccccccccccc,
            int dddddddddddddddd);
    }

    object M11(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int cccccccccccccccccccccccccccccc
            , int dddddddddddddddd);
    }

    object M12(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, int
            ccccccccccccccccccccccccccccccc, int dddddddddddddddd);
    }

    object M13(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccc,
            int dddddddddddddddd);
    }

    object M14(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccccccc
            , int dddddddddddddddd);
    }

    object M15(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb,
            ccccccccccccccccccccccccccccccccccc, int dddddddddddddddd);
    }

    object M16(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(),
            int dddddddddddddddd);
    }

    object M17(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC()
            , int dddddddddddddddd);
    }

    object M18(object owner) {
        return owner is (int aaaaaaaaaaaaaaaaaaaaaaa, int bbbbbbbbbbbbbbbbbbbbbbbbbb,
            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC(), int dddddddddddddddd);
    }

    void M19() {
        var (aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccccccccccccccccccccccccccccccccccccc,
            dddddddddddddddd) = Get();
    }

    void M20() {
        var (aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccccccccccccccccccccccccccccc
            , dddddddddddddddd) = Get();
    }

    void M21() {
        var (aaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb,
            ccccccccccccccccccccccccccccccccccccccccccccccccccccccc, dddddddddddddddd) = Get();
    }
}
