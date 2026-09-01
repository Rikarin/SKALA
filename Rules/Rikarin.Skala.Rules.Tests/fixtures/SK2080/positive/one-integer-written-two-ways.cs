using System.Collections.Generic;

public sealed class Codes {
    public static readonly Dictionary<long, string> Names = new() {
        [1] = "one",
        [2] = "two",
        [1L] = "uno"
    };
}
