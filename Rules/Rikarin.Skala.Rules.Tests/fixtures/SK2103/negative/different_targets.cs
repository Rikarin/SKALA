using System;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.ReturnValue, AllowMultiple = true)]
sealed class TagAttribute : Attribute {
    public TagAttribute(string name) => Name = name;

    public string Name { get; }
}

// ⚠ `[method:]` and `[return:]` are applications to two different things that happen to be
// written on one declaration, so the target is part of the group's identity.
static class Service {
    [method: Tag("audit")]
    [return: Tag("audit")]
    public static int Run() => 0;
}
