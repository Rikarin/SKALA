public readonly struct ParseException {
    public ParseException(int offset) {
        Offset = offset;
    }

    public int Offset { get; }
}
