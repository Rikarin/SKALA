using System.Diagnostics;

// The other half of the position: a branch the author asserts cannot be reached has its own type,
// and reaching for it is what the rule is asking of anybody who does not owe an implementation.
public sealed class Router {
    public string Name(int kind) =>
        kind switch {
            0 => "read",
            1 => "write",
            _ => throw new UnreachableException()
        };
}
