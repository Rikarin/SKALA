using System;

// ⚠ #397: a partial member's note is written above its definition, and the throw is in its
// implementation. The reference is read from either half, because they are one member.
public sealed partial class Cart {
    /// <summary>Not implemented yet: https://github.com/Rikarin/SKALA/issues/412</summary>
    public partial decimal Subtotal { get; }

    public partial decimal Subtotal => throw new NotImplementedException();
}
