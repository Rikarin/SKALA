// #610, SK-DIV-0450: an `=` behind a head of five columns or fewer stays, and the type argument list after it
// fills, once the list's `>` is past the margin or the line is one column over; a head of six or more breaks it.
class C {
    void M1() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M2() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M3() {
        v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M4() {
        v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M5() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M6() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M7() {
        vvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M8() {
        vvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M9() {
        vvv = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M10() {
        vvv = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M11() {
        vvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M12() {
        vvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M13() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M14() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M15() {
        vvvv = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M16() {
        vvvv = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M17() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M18() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M19() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M20() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M21() {
        T v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M22() {
        T v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M23() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M24() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M25() {
        var v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M26() {
        var v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M27() {
        var v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M28() {
        var v = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M29() {
        var v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M30() {
        var v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M31() {
        a.b = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M32() {
        a.b = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M33() {
        a.b = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M34() {
        a.b = Make<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>();
    }

    void M35() {
        a.b = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M36() {
        a.b = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC>();
    }

    void M37() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M38() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M39() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M40() {
        v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M41() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M42() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M43() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M44() {
        vvvv = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M45() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M46() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M47() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M48() {
        T v = new Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Dddddddddd>(capacity);
    }

    void M49() {
        v = default(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddd>);
    }

    void M50() {
        v = default(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddd>);
    }

    void M51() {
        vvvv = default(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddd>);
    }

    void M52() {
        vvvv = default(Dictionary<Aaaaaaaaaaaaaaaaaaaaaaaaa, Bbbbbbbbbbbbbbbbbbbbbbb, CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC, Ddddddddd>);
    }
}
