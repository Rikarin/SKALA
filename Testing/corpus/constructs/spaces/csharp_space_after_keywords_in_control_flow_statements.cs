class C {
    void M(bool b, int[] xs) {
        if (b) {
            M(b, xs);
        }

        foreach (var x in xs) {
            M(b, xs);
        }

        for (var i = 0; i < xs.Length; i++) {
            M(b, xs);
        }
    }
}
