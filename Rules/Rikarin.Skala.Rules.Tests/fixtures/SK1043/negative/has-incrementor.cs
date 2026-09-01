public sealed class Incremented {
    public static int Walk(int limit) {
        var i = 0;
        for (; i < limit; i++) { }

        return i;
    }
}
