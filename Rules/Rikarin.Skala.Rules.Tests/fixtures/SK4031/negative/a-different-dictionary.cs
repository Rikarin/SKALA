using System;
using System.Collections.Generic;

public sealed class Report {
    public static void Write(Dictionary<string, int> keysOf, Dictionary<string, int> valuesOf) {
        foreach (var key in keysOf.Keys) {
            Console.WriteLine(valuesOf[key]);
        }
    }
}
