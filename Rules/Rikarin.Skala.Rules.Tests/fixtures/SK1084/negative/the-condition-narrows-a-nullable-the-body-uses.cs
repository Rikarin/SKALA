using System.Collections.Generic;
using System.Linq;

// ⚠ #329 defect 1: `option.Default is not null` narrows for the *body* of the `if`, and a `Where`
// predicate narrows nothing. Moving it turns `Strip(option.Default)` into CS8604 — the shape that
// broke this repository's build in `OptionsGenerator.cs` when the fix was applied.
public sealed class Option {
    public string? Default { get; init; }
}

public sealed class Generator {
    public static string Strip(string text) => text;

    public static void Emit(IEnumerable<Option> options) {
        foreach (var option in options) {
            if (option.Default is not null) {
                System.Console.WriteLine(Strip(option.Default));
            }
        }
    }
}
