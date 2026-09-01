enum Mode { Fast, Careful }

class C {
    void M(Mode mode) {
        if (mode == Mode.Fast) {
            Fast();
        } else if (mode == Mode.Fast) {
            Careful();
        }
    }

    static void Fast() { }

    static void Careful() { }
}
