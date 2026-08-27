class C {
    void M(bool b) {
        if (b) {
            M(b);
        }
        M(b);
    }
}
