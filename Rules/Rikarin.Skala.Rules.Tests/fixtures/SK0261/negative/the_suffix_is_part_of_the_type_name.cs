// ⚠ #424: `[MarkAttribute]` binds `MarkAttributeAttribute` through the appended suffix, and `[Mark]`
// looks for `Mark` and `MarkAttribute` — neither exists — which is CS0246 (#412's audit).
using System;

[AttributeUsage(AttributeTargets.All)]
public sealed class MarkAttributeAttribute : Attribute { }

[MarkAttribute]
public sealed class Target { }

public static class Probe {
    public static int Run() => typeof(Target).GetCustomAttributes(false).Length;
}
