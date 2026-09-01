using System;

public sealed class Isolated {
    // ⚠ A `static` local function and a `static` lambda may not name their enclosing parameters, so
    // the walk stops there. `nameof(value)` inside `Validate` would be a repair that does not
    // compile, and withholding the finding is cheaper than shipping one.
    public void Write(string value) {
        Validate("x");

        static void Validate(string text) {
            if (text is null) {
                throw new ArgumentNullException("valeu");
            }
        }
    }
}
