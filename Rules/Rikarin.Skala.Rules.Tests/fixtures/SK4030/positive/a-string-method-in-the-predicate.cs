// #430: a framework method on text, handed only text and an enum, runs no code that could reach the list.
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    public static bool AnyHidden(List<string> names) =>
        names.Any(name => name.StartsWith(".", StringComparison.Ordinal) && name.Length > 1);
}
