// ⚠ A lambda body is a separate reading context, exactly as it is for `SK7006`'s nesting depth.
// The conditional inside the `Select` is not nested inside the outer one in any sense a reader
// experiences, so neither expression is over the default threshold.
using System.Collections.Generic;
using System.Linq;

namespace Fixtures;

class Projection {
    public static IEnumerable<string> Render(bool empty, IEnumerable<int> values) =>
        empty ? Enumerable.Empty<string>() : values.Select(static value => value > 0 ? "up" : "down");
}
