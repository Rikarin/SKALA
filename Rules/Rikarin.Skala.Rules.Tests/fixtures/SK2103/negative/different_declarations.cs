using System;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// The same attribute on two different members is two applications to two different things.
static class Service {
    [Tag("audit")]
    public static void First() { }

    [Tag("audit")]
    public static void Second() { }
}
