class C {
    void M(bool ready) {
        var flag = false;
        flag =! ready;
        Use(flag);
    }

    static void Use(bool value) { }
}
