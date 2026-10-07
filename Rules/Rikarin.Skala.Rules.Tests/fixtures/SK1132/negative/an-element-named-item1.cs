// An element literally named `Item1` at position 1: the rename would be a no-op edit.
public static class Odd {
    public static int Of((int Item1, int Count) value) => value.Item1 + value.Count;
}
