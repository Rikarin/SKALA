// ⚠ #302's shape (#325), on this rule's `Enum.Member.ToString()` branch — a second call site of the
// same guard, moved with it. The comment sits in the invocation's leading trivia; the fix rewrites
// only `Colour.Red.ToString()` into `nameof(Colour.Red)`.
public enum Colour {
    Red,
    Green
}

public sealed class Palette {
    public string Label() =>
        // the enum member's own name, spelled at run time for no reason
        Colour.Red.ToString();
}
