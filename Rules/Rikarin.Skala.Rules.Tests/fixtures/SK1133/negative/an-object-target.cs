// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Boxed {
    public object Copy(IEnumerable<int> source) {
        object copied = source.ToArray();
        return copied;
    }
}
