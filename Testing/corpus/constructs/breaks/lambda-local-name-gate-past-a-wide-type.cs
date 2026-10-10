// #595's residue, SK-DIV-0444: a lambda-valued local's name gate falls again past a type of 60 columns, to a
// floor of ten; `static` changes nothing.
class C595g {
    void M1() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvv = x => sssssssssssssssssssssssssssssssssss;
    }

    void M2() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvvv = x => ssssssssssssssssssssssssssssssssss;
    }

    void M3() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvv = static x => ssssssssssssssssssssssssssss;
    }

    void M4() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvvv = static x => sssssssssssssssssssssssssss;
    }

    void M5() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvv = x => ssssssssssssssssssssssssssssssss;
    }

    void M6() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvv = x => sssssssssssssssssssssssssssssss;
    }

    void M7() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvv = static x => sssssssssssssssssssssssss;
    }

    void M8() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvvvvv = static x => ssssssssssssssssssssssss;
    }

    void M9() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = x => sssssssssssssssssssssssssss;
    }

    void M10() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvv = x => ssssssssssssssssssssssssss;
    }

    void M11() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = static x => ssssssssssssssssssss;
    }

    void M12() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvvv = static x => sssssssssssssssssss;
    }

    void M13() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvv = x => ssssssssssssssssssssssss;
    }

    void M14() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = x => sssssssssssssssssssssss;
    }

    void M15() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvv = static x => sssssssssssssssss;
    }

    void M16() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = static x => ssssssssssssssss;
    }

    void M17() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvv = x => ssssssssssssssss;
    }

    void M18() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = x => sssssssssssssss;
    }

    void M19() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvv = static x => sssssssss;
    }

    void M20() {
        Func<TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT, int> vvvvvvvvvvv = static x => ssssssss;
    }
}
