public sealed class Inner {
    public string? Name;
}

public sealed class Outer {
    Inner Child() => new();

    public void Ensure() {
        Child().Name = Child().Name ?? "anonymous";
    }
}
