public sealed class Initialised {
    public static int Walk(int limit) {
        int i;
        for (i = 0; i < limit;) {
            i += 2;
        }

        return i;
    }
}
