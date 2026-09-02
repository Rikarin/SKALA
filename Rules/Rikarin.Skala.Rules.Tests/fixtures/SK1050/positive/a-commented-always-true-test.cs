// ⚠ #302's shape (#325), and the cleanest demonstration in this batch: it is the existing positive
// `type-check-that-always-succeeds.cs` with its comment moved ONE LINE DOWN. Above the method the
// comment is outside the guarded node and the rule fires; inside the expression it lands in the
// node's leading trivia and the rule went silent. Nothing else about the code changed.
public sealed class Inspector {
    public bool Present(string? value) =>
        // `value` is already a `string`, so the only thing this can discover is whether it is null.
        value is object;
}
