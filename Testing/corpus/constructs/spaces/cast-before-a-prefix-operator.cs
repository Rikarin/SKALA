// #591, SK-DIV-0441: the gap between a cast's `)` and a prefix operator is the cast's, `space_after_cast`.
class C {
    void M(int x, bool b, int a) {
        var a1 = (int)-1;
        var a2 = (int) -1;
        var a3 = (int)~x;
        var a4 = (int) ~x;
        var a5 = (bool)!b;
        var a6 = (bool) !b;
        var a7 = (long)+x;
        var a8 = (long) +x;
        var a9 = (int)++x;
        var b1 = a - (int)-x;
        var b2 = (a) - x;
        var b3 = (a)-x;
    }
}
