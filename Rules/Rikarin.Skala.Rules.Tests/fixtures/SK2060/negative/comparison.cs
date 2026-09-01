class C {
    void M(bool ready, bool pending) {
        if (ready == pending) {
            Start();
        }

        while (ready != pending) {
            Start();
        }
    }

    static void Start() { }
}
