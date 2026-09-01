// analyzer-option: dotnet_code_quality.SK7083.threshold = 1
// ⚠ Two places a literal is written that are not repeats of a decision.
//
// A `const` initialiser is the repair the rule asks for — counting it means the rule still fires
// after somebody did exactly what it wanted, which is the fastest way to teach people it is noise.
//
// An attribute argument has to be a compile-time constant, so the only extraction available is a
// `const` the attribute then names; the arguments that actually repeat are obsolescence messages
// and display names, where writing the string out at the declaration is the point.
using System;

namespace Fixtures;

class Names {
    const string Local = "tenant-id";

    const string Header = "tenant-id";

    [Obsolete("tenant-id")]
    public static string One() => Local;

    [Obsolete("tenant-id")]
    public static string Two() => Header;

    [Obsolete("tenant-id")]
    public static string Three() => Local;

    [Obsolete("tenant-id")]
    public static string Four() => Header;
}
