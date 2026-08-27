using System;

public enum TokenKind {
    None,
    Identifier
}

public sealed class Holder {
    public static Array All() {
        Array values = Enum.GetValues(typeof(TokenKind));
        return values;
    }
}
