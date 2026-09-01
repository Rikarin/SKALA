public static class Points {
    public static int Sum((int X, int Y) point) {
        var (x, y) = point;

        return x + y;
    }

    public static int Half((int X, int Y) point) {
        var (first, _) = point;

        return first;
    }
}
