class C {
    void M(int? count) {
        if (count! is not int) {
            Handle();
        }
    }

    static void Handle() { }
}
