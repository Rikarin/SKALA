using System;

public sealed class Indirect {
    const string Name = "vlaue";

    public void Check(string value, string vlaue) {
        if (value is null) {
            throw new ArgumentNullException(Name);
        }

        if (value.Length == 0) {
            throw new ArgumentNullException(vlaue);
        }

        if (value.Length > 8) {
            throw new ArgumentNullException($"{Name}");
        }

        // An empty `paramName` is a deliberate "no parameter", not a misspelt one.
        throw new ArgumentException("rejected", "");
    }
}
