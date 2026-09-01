using System.Collections.Generic;

public sealed class Limits {
    static int Read(Dictionary<string, int> source) => source["read"];

    // The intervening write's value is an invocation, and an invocation is a place the first value
    // can be read from. The run ends there.
    public static void Configure(Dictionary<string, int> limits) {
        limits["read"] = 100;
        limits["write"] = Read(limits);
        limits["read"] = 25;
    }
}
