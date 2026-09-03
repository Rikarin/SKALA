class C {
    void M(int[] xs) {
        foreach (var x in xs)
        foreach (var y in xs) {
            M(xs);
        }
    }
}
