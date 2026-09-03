class C {
    int M(int a) {
        var x = 1;
        var y = 2;
        return x + y;
    }

    void N(int a) {
        for (var i = 0; i < a; i++) {
            a++;
            continue;
        }
    }
}
