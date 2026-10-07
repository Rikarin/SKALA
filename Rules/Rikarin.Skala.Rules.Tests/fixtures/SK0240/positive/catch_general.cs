class C {
    static readonly int[] Values = new int[4];

    public static int Sum(int first, int second) {
        var total = 0;
        try {
            total = Values[first];
            total += Values[second];
        } catch {
            throw;
        }

        return total;
    }
}
