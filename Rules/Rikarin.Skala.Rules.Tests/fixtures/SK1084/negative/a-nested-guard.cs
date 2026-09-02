using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ Reporting this would make the rule fire on its own output, and `skala fix` would apply two
    // rewrites where a person should make one deliberately.
    public static void Render(IEnumerable<int> numbers) {
        foreach (var number in numbers) {
            if (number > 0) {
                if (number < 10) {
                    System.Console.WriteLine(number);
                }
            }
        }
    }
}
