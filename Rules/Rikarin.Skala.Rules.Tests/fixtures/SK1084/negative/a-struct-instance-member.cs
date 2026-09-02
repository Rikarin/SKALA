using System.Collections.Generic;
using System.Linq;

public struct Registry {
    int limit;

    // ⚠ CS1673: a lambda inside a struct cannot reach `this`, and a bare member name is a `this`
    // reference with no token to match on.
    public void Render(IEnumerable<int> numbers) {
        foreach (var number in numbers) {
            if (number > this.limit) {
                System.Console.WriteLine(number);
            }
        }
    }
}
