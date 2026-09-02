using System;

public sealed class Registry {
    public Array All<T>() where T : struct, Enum => Enum.GetValues(typeof(T));
}
