using System;

namespace Contoso.Design;

// Read from the field at run time, so a caller that is not rebuilt still sees the new value. This is
// what the rule's advice points at.
public static class Limits {
    public static readonly int MaxRetries = 3;

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
}
