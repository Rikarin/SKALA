using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static void Render(IEnumerable<int> numbers) {
        foreach (var number in numbers) {
            if (number > 0) {
                System.Console.WriteLine(number);
                System.Console.WriteLine(number * 2);
            }
        }
    }
}
