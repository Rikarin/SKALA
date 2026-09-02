using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // A constant filter is a condition a reader would rather see hoisted out of the loop entirely.
    public static void Render(IEnumerable<int> numbers, bool enabled) {
        foreach (var number in numbers) {
            if (enabled) {
                System.Console.WriteLine(number);
            }
        }
    }
}
