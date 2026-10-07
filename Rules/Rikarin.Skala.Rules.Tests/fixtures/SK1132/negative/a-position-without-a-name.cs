// A partially named tuple: position 1 has no name, so `Item1` is its name. Position 2 is read by name.
public static class Labels {
    public static string Of((int, string Name) label) => label.Item1 + label.Name;
}
