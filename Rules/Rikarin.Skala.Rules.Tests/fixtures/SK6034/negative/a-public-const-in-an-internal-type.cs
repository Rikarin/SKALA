namespace Contoso.Design;

// The field is public and the type is not, so the value never leaves the assembly.
internal static class Internals {
    public const int MaxRetries = 3;
}

internal sealed class Outer {
    public sealed class Nested {
        public const int Depth = 2;
    }
}
