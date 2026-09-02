public sealed class Cursor {
    int position;

    public int Next(int[]? source) => source?[position++] ?? -1;
}
