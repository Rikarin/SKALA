using System.Collections.Generic;

public sealed class Weights {
    // `double` is outside the decidable key types. The duplicate is real; the rule declines rather
    // than argue about what `-0.0` and `NaN` mean to `EqualityComparer<double>.Default`.
    public static readonly Dictionary<double, string> Names = new() {
        [1.0] = "one",
        [1.0] = "uno"
    };
}
