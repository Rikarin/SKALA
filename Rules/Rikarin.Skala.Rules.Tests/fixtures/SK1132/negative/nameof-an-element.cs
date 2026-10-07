// ⚠ `nameof(t.Item1)` is the string "Item1". Renaming inside it changes a value, not a spelling.
public static class Names {
    public static string Of((int Count, string Name) t) => nameof(t.Item1);
}
