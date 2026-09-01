using System;

// ⚠ An expression-bodied member has no enclosing statement, so the reference has to be found on
// the member itself. It is the place a reader is already looking.
public sealed class Cart {
    /// <summary>Not implemented yet: https://github.com/Rikarin/SKALA/issues/412</summary>
    public decimal Subtotal => throw new NotImplementedException();
}
