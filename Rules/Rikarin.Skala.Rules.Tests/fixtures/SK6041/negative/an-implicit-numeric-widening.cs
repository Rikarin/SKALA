using System.Collections.Generic;

public static class Widths {
    public static long Sum(List<int> numbers) {
        long total = 0;
        foreach (long number in numbers) {
            total += number;
        }

        return total;
    }
}
