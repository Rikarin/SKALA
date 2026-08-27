class C {
    void M(bool b) {
        start:
        if (b) {
            goto start;
        }
    }
}
