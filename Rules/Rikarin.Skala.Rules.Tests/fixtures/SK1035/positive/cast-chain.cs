using System;
using System.Linq;

public enum TokenKind {
    None,
    Identifier
}

public sealed class Holder {
    public static TokenKind[] All() => Enum.GetValues(typeof(TokenKind)).Cast<TokenKind>().ToArray();
}
