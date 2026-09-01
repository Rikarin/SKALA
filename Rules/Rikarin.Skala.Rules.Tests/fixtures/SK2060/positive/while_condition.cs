class C {
    bool running;

    void M() {
        while (running = ShouldRun()) {
            Step();
        }
    }

    static bool ShouldRun() => false;

    static void Step() { }
}
