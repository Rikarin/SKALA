using System;

public struct Point { }

public sealed class Registry {
    public object Read(string text) => Enum.Parse(typeof(Point), text);
}
