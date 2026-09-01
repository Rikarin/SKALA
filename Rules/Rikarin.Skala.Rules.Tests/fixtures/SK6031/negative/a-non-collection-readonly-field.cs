using System;

namespace Contoso.Design;

// `readonly` on a value or on an immutable reference type is the modifier doing exactly what it
// says, and it is the overwhelming majority of every `readonly` ever written.
public sealed class Limits {
    public readonly int Max = 10;

    public readonly string Name = "limits";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public readonly object Gate = new();
}
