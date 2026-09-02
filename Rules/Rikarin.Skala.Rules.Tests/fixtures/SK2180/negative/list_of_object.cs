using System.Collections.Generic;

static class Untyped {
    // `List<object>` is excluded by the same test as `ArrayList`, deliberately: both hand the loop
    // an element the author cannot type.
    public static int Length(List<object> values) {
        var total = 0;
        foreach (string value in values) {
            total += value.Length;
        }

        return total;
    }
}
