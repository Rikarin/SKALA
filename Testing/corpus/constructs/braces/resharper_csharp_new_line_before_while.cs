class C {
    void M(bool b) {
        do {
            M(b);
        }
        while (b);
    }
}
