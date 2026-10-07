// ⚠ #424: with braces `{ x = 2 }` is a call to `Add(x = 2)`; without them an assignment in a
// collection initializer is CS0747 (#412's audit).
using System.Collections.Generic;

public static class Probe {
    public static string Run() {
        var x = 0;
        var l = new List<int> { 1, { x = 2 } };
        return l.Count + " " + x;
    }
}
