// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Names {
    public List<string> Copy(IEnumerable<string> names) {
        List<string> copied = names.ToList();
        return copied;
    }
}
