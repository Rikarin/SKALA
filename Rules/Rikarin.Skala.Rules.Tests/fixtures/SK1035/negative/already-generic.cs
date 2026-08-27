using System;

public enum TokenKind {
    None,
    Identifier
}

public sealed class Holder {
    public static TokenKind[] All() => Enum.GetValues<TokenKind>();
}
