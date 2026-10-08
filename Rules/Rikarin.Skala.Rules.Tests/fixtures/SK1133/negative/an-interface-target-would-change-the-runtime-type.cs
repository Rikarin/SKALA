// fixture-option: LangVersion = 14
// `IEnumerable<int> e = [..source];` lets the compiler choose the implementation; `e is int[]` stops
// holding.
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    public static string Run() {
        IEnumerable<int> source = new List<int> { 1 };
        IEnumerable<int> copied = source.ToArray();
        IReadOnlyList<int> listed = source.ToList();
        return (copied is int[]) + "/" + (listed is List<int>);
    }
}
