using System;

public enum Kind { First, Second }

public sealed class Registry {
    public bool Known(int value) => Enum.IsDefined(typeof(Kind), value);
}
