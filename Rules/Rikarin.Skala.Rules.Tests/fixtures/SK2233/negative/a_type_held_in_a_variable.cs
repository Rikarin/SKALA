// The `Type` arrived from a caller. What it holds is not a fact in this file, which is the same
// reason SK5001 refuses to treat a parameter as a source.
using System;

public sealed class Registry {
    public Array All(Type kind) => Enum.GetValues(kind);
}
