using System;

namespace Contoso.Design;

// No element exists for anybody to write, so `readonly` really is the whole guarantee.
public sealed class Empties {
    public static readonly string[] None = [];

    public static readonly int[] Nothing = Array.Empty<int>();

    public static readonly byte[] Zero = new byte[0];
}
