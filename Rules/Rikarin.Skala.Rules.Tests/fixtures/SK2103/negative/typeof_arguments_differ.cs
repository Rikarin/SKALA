using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
sealed class HandlesAttribute : Attribute {
    public HandlesAttribute(Type type) => Type = type;

    public Type Type { get; }
}

// A `typeof` argument is not a constant, so it is compared as a type symbol rather than declined.
[Handles(typeof(int))]
[Handles(typeof(long))]
sealed class Router { }
