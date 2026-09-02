class C {
    void M(bool a, bool b) {
        if (a)
            if (b)
                Inner();
        After();
    }

    static void Inner() { }

    static void After() { }
}
