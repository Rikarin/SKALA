// The assignment is an operand deep inside the condition, not the condition itself.
class C {
    void M() {
        var handle = 0;
        if (Init() && (handle = Open()) != 0) {
            Use(handle);
        }
    }

    static bool Init() => true;

    static int Open() => 1;

    static void Use(int handle) { }
}
