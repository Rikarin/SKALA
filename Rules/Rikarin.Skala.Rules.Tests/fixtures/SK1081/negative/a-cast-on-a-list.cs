using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ Deleting this changes the expression's static type from IEnumerable<string> to List<string>,
    // which a `var` declaration or an overload set can see. SK0234 records the same trap for casts.
    public static IEnumerable<string> Names(List<string> source) => source.Cast<string>();
}
