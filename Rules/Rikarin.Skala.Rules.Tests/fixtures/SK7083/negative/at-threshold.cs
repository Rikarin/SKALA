// Exactly five occurrences. The family reports `> threshold`, so the threshold itself is silent —
// this is the fixture that proves the boundary is not off by one.
using System;

namespace Fixtures;

class Headers {
    public static bool Has(string name) => name == "tenant-id";

    public static bool Matches(string name) => name.Equals("tenant-id", StringComparison.Ordinal);

    public static bool Starts(string name) => name.StartsWith("tenant-id", StringComparison.Ordinal);

    public static bool Ends(string name) => name.EndsWith("tenant-id", StringComparison.Ordinal);

    public static string Name() => "tenant-id";
}
