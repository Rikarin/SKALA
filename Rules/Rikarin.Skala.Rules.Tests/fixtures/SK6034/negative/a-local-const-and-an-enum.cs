namespace Contoso.Design;

// A local constant is not a field and is copied nowhere; an enum member is not a `const` field
// either, and its values already carry the same versioning caveat by design.
public static class Rates {
    public static int Scale(int value) {
        const int Factor = 7;

        return value * Factor;
    }
}

public enum Severity {
    None = 0,
    Warning = 1
}
