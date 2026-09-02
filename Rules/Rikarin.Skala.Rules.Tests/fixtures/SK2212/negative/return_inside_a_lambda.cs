// ⚠ The `return` belongs to the lambda, not to the loop. The same region analysis excludes it,
// and a rule that counted `return` tokens would not.
using System.Collections.Generic;
using System.Linq;

class C {
    void M(List<List<int>> groups) {
        foreach (var group in groups) {
            var positive = group.Where(x => {
                return x > 0;
            }).ToList();

            System.Console.WriteLine(positive.Count);
        }
    }
}
