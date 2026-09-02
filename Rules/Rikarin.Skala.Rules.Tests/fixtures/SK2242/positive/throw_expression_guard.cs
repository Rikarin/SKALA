using System;
using System.Collections.Generic;

namespace Fixtures.SK2242;

public static class ThrowExpressionGuard {
    public static IEnumerable<string> Lines(string? text) {
        var checked_ = text ?? throw new ArgumentNullException(nameof(text));

        yield return checked_;
    }
}
