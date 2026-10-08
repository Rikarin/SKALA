// fixture-option: LangVersion = 14
// The arrow is a statement here: the value is discarded and `[..source]` is not a statement expression.
using System.Collections.Generic;
using System.Linq;

public sealed class Discarded {
    public void Touch(IEnumerable<int> source) => source.ToArray();
}
