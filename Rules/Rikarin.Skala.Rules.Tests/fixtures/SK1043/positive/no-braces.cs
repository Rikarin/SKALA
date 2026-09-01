public sealed class Counting {
    public static int Depth(int start, int limit) {
        var value = start;
        for (; value < limit;) value++;
        return value;
    }
}
