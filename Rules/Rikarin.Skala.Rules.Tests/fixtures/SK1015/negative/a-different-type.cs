using System.Collections;
using System.Collections.Generic;

// `is IList` and `(IList<int>)` share a prefix and nothing else: the two conversions can succeed
// and fail independently, so the cast is not the one the test proved.
public sealed class Visitor {
    public int Visit(object value) {
        if (value is IList) {
            var list = (IList<int>)value;
            return list.Count;
        }

        return 0;
    }
}
