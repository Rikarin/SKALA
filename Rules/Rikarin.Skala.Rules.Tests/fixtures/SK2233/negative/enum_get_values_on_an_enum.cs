using System;

public enum Kind { First, Second }

public sealed class Registry {
    public Array All() => Enum.GetValues(typeof(Kind));
}
