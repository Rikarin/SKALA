public ref struct Cursor {
    public int Position;
}

public ref struct Walk {
    Cursor cursor;

    public Walk(int start) {
        cursor = new Cursor();
        cursor.Position = start;
    }

    public int Position => cursor.Position;
}
