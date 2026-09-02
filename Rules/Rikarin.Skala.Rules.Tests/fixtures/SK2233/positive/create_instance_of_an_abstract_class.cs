using System;

public abstract class Shape { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(Shape));
}
