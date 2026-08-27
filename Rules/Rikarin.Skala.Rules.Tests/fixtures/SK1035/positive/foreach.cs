public enum TokenKind {
    None,
    Identifier
}

public sealed class Holder {
    public static int Count() {
        var total = 0;
        foreach (TokenKind kind in System.Enum.GetValues(typeof(TokenKind))) {
            total++;
        }

        return total;
    }
}
