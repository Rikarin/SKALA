// fixture-option: LangVersion = 14
// ⚠ The issue expected `First([..source])` not to compile. It does: a collection expression takes part
// in type inference through its spread's element type, so `T` is `int` either way and the call binds to
// `First<int>` — which FixRebind compares exactly, type arguments included. Pinned by Probe.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static string First<T>(T[] values) => typeof(T).Name + ":" + values[0];

    public static string Run() {
        IEnumerable<int> source = new List<int> { 7, 8 };
        return First(source.ToArray());
    }
}
