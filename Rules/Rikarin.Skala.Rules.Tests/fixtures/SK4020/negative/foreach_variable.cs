using System;
using System.Collections.Generic;

static class ForeachCaptureFixture {
    public static List<Func<int>> Build(IEnumerable<int> values) {
        var result = new List<Func<int>>();
        foreach (var value in values) {
            result.Add(() => value);
        }

        return result;
    }
}
