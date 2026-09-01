// One name, two `out` positions: an inline declaration in both would declare it twice.
public sealed class Reader {
    static void Split(out int first, out int second) {
        first = 1;
        second = 2;
    }

    public void Run() {
        int value;
        Split(out value, out value);
    }
}
