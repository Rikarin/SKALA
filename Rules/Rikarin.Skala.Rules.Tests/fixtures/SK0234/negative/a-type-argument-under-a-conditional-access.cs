using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // ⚠ `.Where(…).Cast<string>()` is the `WhenNotNull` of a conditional access. Replacing the
    // type argument list and re-binding the result detaches the member binding from the access
    // that is its receiver, and Roslyn's own FindConditionalAccessNodeForBinding dereferences
    // null — a NullReferenceException thrown by the compiler inside the analyzer, reported as
    // AD0001 and therefore as nothing. Found by asserting on AD0001, not by reading the code.
    public static IEnumerable<string>? Names(IEnumerable<object>? values) =>
        values?.Where(value => value is string).Cast<string>();
}
