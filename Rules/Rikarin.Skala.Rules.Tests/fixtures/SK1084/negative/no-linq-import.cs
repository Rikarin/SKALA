using System.Collections.Generic;

public sealed class Registry {
    // The fix does not add a using directive, so a file without System.Linq is left alone.
    public static void Render(IEnumerable<int> numbers) {
        foreach (var number in numbers) {
            if (number > 0) {
                System.Console.WriteLine(number);
            }
        }
    }
}
