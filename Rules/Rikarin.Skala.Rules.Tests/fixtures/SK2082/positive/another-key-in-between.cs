using System.Collections.Generic;

public sealed class Limits {
    public static void Configure(Dictionary<string, int> limits) {
        limits["read"] = 100;
        limits["write"] = 50;
        limits["read"] = 25;
    }
}
