// ⚠ `if (Read()) … else if (Read())` really is two different questions.
class C {
    void M() {
        if (Read()) {
            First();
        } else if (Read()) {
            Second();
        }
    }

    static bool Read() => false;

    static void First() { }

    static void Second() { }
}
