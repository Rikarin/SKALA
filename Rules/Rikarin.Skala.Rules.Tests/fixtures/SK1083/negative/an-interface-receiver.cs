using System.Collections.Generic;

public sealed class Registry {
    // IList<T> promises no enumeration order, so a `foreach` over one could visit a different sequence.
    public static int Total(IList<int> numbers) {
        var total = 0;
        for (var i = 0; i < numbers.Count; i++) {
            total += numbers[i];
        }

        return total;
    }
}
