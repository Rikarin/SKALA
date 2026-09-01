namespace Contoso.Design;

// ⚠ `nameof(parameter)` is a compile-time constant that reads its input. It is the one shape where
// both halves of this rule's predicate hold and the finding is still wrong, so it is excluded by
// name rather than left to the constant test.
public sealed class Diagnostics {
    public string Describe(int attempt) => Label(attempt);

    static string Label(int attempt) => nameof(attempt);
}
