using System;

public sealed class Widget { }

public sealed class Registry {
    public string? Describe() => typeof(Widget).FullName;

    public bool Same(object value) => value.GetType() == typeof(Widget);
}
