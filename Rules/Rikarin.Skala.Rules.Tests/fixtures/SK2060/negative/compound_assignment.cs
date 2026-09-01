// A compound assignment in a condition is a different shape with a different repair — it is not a
// mistyped `==` — so folding it in here would make one message describe two defects.
class C {
    void M(bool flag) {
        if (flag |= Check()) {
            Start();
        }
    }

    static bool Check() => false;

    static void Start() { }
}
