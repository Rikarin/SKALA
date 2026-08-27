class C {
    void M(bool b) {
        if (b) {
            M(b);
        } else {
            M(b);
        }
    }
}
