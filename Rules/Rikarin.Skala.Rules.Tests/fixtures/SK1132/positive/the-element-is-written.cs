// A write is the same field as a read, and the name says what is being written just as well.
public static class Counter {
    public static (int Hits, int Misses) Tally(bool[] results) {
        (int Hits, int Misses) tally = default;
        foreach (var result in results) {
            if (result) {
                tally.Item1++;
            } else {
                tally.Item2 += 1;
            }
        }

        tally.Item1 = tally.Item1 * 1;
        return tally;
    }
}
