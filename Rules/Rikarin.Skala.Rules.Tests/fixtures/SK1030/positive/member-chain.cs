public sealed class Inner {
    public string? Name;
}

public sealed class Outer {
    public Inner Child = new();

    public void Ensure() {
        Child.Name = Child.Name ?? "anonymous";
    }
}
