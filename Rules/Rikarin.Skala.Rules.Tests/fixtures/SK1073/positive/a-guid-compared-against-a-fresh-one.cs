using System;

public sealed class Registry {
    public bool IsUnset(Guid id) => id == new Guid();
}
