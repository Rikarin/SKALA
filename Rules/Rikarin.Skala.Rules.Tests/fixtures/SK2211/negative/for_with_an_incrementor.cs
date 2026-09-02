// A `for` with any incrementor is declined — that is where the change usually is, and reading it
// is a value question this rule does not ask.
class C {
    void M(int count) {
        for (var i = 0; i < count; i += Step()) {
            System.Console.WriteLine(i);
        }
    }

    static int Step() => 1;
}
