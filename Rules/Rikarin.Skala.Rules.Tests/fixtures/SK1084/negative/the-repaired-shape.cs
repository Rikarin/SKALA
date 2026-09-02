using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static void Render(IEnumerable<int> numbers) {
        foreach (var number in numbers.Where(number => number > 0)) {
            System.Console.WriteLine(number);
        }
    }
}
