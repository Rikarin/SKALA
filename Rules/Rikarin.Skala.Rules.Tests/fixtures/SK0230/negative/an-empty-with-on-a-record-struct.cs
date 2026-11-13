public record struct Cursor(int Line, int Column) {
    public void Advance() => Column++;
}

public static class Cursors {
    // A record struct's copy is a value, and it is still not the variable: the empty `with` advances
    // a temporary, and `cursor.Advance()` would advance the caller's cursor.
    public static int Peek(Cursor cursor) {
        var ahead = cursor with { };
        ahead.Advance();
        return ahead.Column;
    }
}
