using System;

public interface IWidget { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(IWidget));
}
