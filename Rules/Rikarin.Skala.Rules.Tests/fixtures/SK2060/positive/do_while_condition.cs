class C {
    void M(bool more) {
        do {
            Step();
        } while (more = Next());
    }

    static bool Next() => false;

    static void Step() { }
}
