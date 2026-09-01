class C {
    void M() {
        var remaining = -1;
        var total = +2;
        var flag = !remaining.Equals(0);
        Use(remaining + total, flag);
    }

    static void Use(int value, bool flag) { }
}
