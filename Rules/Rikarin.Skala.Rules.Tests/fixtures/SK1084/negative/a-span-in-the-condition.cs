using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ CS8175: a ref struct cannot be captured, so the lambda the fix writes would not compile.
    public static void Render(IEnumerable<int> numbers) {
        Span<int> limits = stackalloc int[2];
        limits[0] = 3;
        foreach (var number in numbers) {
            if (number > limits[0]) {
                System.Console.WriteLine(number);
            }
        }
    }
}
