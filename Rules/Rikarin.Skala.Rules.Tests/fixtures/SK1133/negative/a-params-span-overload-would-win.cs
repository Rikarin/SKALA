// fixture-option: LangVersion = 14
// ⚠ `M(source.ToArray())` binds the `params int[]` overload in its normal form; `M([..source])` binds
// `params ReadOnlySpan<int>`, the better conversion for a collection expression. Rebinding refuses it.
using System;
using System.Collections.Generic;
using System.Linq;

public static class Probe {
    static string M(params int[] values) => "array";

    static string M(params ReadOnlySpan<int> values) => "span";

    public static string Run() {
        IEnumerable<int> source = new[] { 1, 2 };
        return M(source.ToArray());
    }
}
