using System;

public sealed class Widget { }

public sealed class Registry {
    public Array All() => Enum.GetValues(typeof(Widget));
}
