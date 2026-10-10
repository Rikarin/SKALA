// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-10
// #559, SK-DIV-0448: a type argument ending exactly at the margin carries its comma to the next line, as a
// positional pattern's element does.

class C {
    void M1() {
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddddddddddd> x;
    }

    void M2() {
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddddddddddd> x;
    }

    void M3() {
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
            , Ddddddddddddddd> x;
    }

    void M4() {
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd> x;
    }

    void M5() {
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd> x;
    }

    void M6() {
        Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddddddddddd>();
    }

    void M7() {
        Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddddddddddd>();
    }

    void M8() {
        Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
            , Ddddddddddddddd>();
    }

    void M9() {
        Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd>();
    }

    void M10() {
        Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd>();
    }

    void P16(
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddd> x
    ) { }

    void P17(
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddd> x
    ) { }

    void P18(
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddd> x
    ) { }

    void P19(
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddd> x
    ) { }

    void P20(
        Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
            Ddddddd> x
    ) { }

    Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
        Ddddddddddddddd> f;

    Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC,
        Ddddddddddddddd> f;

    Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC
        , Ddddddddddddddd> f;

    Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
        CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd> f;

    Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
        CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddddddddd> f;
}
