using System.Collections.Generic;

public static class Absences {
    public static int Count(List<int> numbers) {
        var total = 0;
        foreach (int? number in numbers) {
            if (number.HasValue) {
                total++;
            }
        }

        return total;
    }
}
