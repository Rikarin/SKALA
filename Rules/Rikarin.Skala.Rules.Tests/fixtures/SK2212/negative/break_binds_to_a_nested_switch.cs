// ⚠ The trap a syntactic rule gets wrong: this `break` belongs to the `switch`, not to the loop,
// so the loop keeps running. `AnalyzeControlFlow` over the body region binds every jump to its own
// enclosing statement, which is why the question is asked of control flow rather than of the last
// statement on the page.
using System.Collections.Generic;

class C {
    void M(List<int> items) {
        foreach (var item in items) {
            switch (item) {
                case 1:
                    System.Console.WriteLine("one");
                    break;

                default:
                    System.Console.WriteLine("other");
                    break;
            }
        }
    }
}
