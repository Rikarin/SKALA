using System;

public sealed class Widget { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(Widget));
}
