public static class Inferred {
    public static int Twice(int value) {
        // `var twice = n => n * 2;` has no natural type at all.
        var twice = (int n) => n * 2;
        return twice(value);
    }
}
