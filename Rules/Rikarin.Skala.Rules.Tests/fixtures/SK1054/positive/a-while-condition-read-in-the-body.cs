// A loop-condition variable that nothing captures: a new variable per iteration and one shared variable
// hold the same value at every read. Pinned by Probe (#425).
public static class Probe {
    static int next;

    static bool Next(out int value) {
        value = ++next;
        return value <= 3;
    }

    public static int Run() {
        var total = 0;
        int value;
        while (Next(out value)) {
            total += value;
        }

        return total;
    }
}
