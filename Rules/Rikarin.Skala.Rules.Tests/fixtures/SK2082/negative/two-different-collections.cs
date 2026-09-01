using System.Collections.Generic;

public sealed class Limits {
    public static void Configure(Dictionary<string, int> incoming, Dictionary<string, int> outgoing) {
        incoming["read"] = 100;
        outgoing["read"] = 25;
    }
}
