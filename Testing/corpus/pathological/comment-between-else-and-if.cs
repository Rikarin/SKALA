class C {
    void M(int a) {
        if (a == 1) {
            M(a);
        } else
        // a note
        if (a == 2) {
            M(a);
        }
    }
}
