using System.Collections.Generic;

public static class Explicitly {
    static T First<T>(IEnumerable<T> values) {
        foreach (var value in values) {
            return value;
        }

        return default!;
    }

    // Inference would choose `string`; the explicit `object` is doing work.
    public static object FirstObject(IEnumerable<string> values) => First<object>(values);
}
