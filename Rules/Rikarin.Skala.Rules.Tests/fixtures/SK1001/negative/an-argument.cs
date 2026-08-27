using System.Collections.Generic;

// The parameter's type is written, but `Take(new string[] { … })` and `Take([…])` need not resolve
// to the same overload: a collection expression converts to several collection types at once, so
// the rewrite can change which method runs.
public sealed class Names {
    static void Take(string[] values) => System.Console.WriteLine(values.Length);

    static void Take(List<string> values) => System.Console.WriteLine(values.Count);

    public void Run() {
        Take(new string[] { "a" });
    }
}
