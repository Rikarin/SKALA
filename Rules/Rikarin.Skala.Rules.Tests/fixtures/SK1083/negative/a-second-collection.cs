public sealed class Registry {
    public static int Dot(int[] left, int[] right) {
        var total = 0;
        for (var i = 0; i < left.Length; i++) {
            total += left[i] * right[i];
        }

        return total;
    }
}
