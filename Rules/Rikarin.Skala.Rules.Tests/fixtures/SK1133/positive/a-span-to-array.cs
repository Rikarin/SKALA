// fixture-option: LangVersion = 14
using System;

public static class Probe {
    public static string Run() {
        Span<int> span = stackalloc int[] { 4, 5 };
        int[] copied = span.ToArray();
        ReadOnlySpan<int> empty = default;
        int[] none = empty.ToArray();
        return string.Join(",", copied) + "/" + ReferenceEquals(none, Array.Empty<int>());
    }
}
