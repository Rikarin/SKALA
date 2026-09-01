using System.Diagnostics.Contracts;

// The shape the annotation is for.
static class Validation {
    [Pure]
    public static bool IsValid(string input) => input.Length > 0;
}
