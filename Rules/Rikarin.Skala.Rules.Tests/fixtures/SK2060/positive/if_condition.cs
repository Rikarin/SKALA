class C {
    void M(bool ready, bool pending) {
        if (ready = pending) {
            Start();
        }
    }

    static void Start() { }
}
