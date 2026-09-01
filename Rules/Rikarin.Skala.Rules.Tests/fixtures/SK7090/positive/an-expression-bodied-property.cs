using System;

// ⚠ The shape a model reaches for most readily: the signature is complete, the type checks, and
// the member is a runtime failure with nothing in the source that says so.
public sealed class Cart {
    public decimal Subtotal => throw new NotImplementedException();
}
