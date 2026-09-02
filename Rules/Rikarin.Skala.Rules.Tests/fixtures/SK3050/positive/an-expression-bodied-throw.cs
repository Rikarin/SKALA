using System;

public sealed class Panel {
    // ⚠ A `throw` expression is the same exception on the same context; only the spelling differs.
    public async void Fail() => throw new InvalidOperationException("always");
}
