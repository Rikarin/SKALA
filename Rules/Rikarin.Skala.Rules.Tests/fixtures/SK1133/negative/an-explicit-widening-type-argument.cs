// fixture-option: LangVersion = 14
// `ToList<object>()` on a `List<string>` lowers, as a spread, to a counted copy loop and not to `ToList`.
using System.Collections.Generic;
using System.Linq;

public sealed class Widen {
    public List<object> Box(List<string> names) {
        List<object> boxed = names.ToList<object>();
        return boxed;
    }
}
