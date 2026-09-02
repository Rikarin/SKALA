class C {
    public static int Count(int[] values) {
        var total = 0;
        foreach (var value in values) {
            if (value < 0) {
                continue;
            } else {
                total += value;
            }
        }

        return total;
    }
}
