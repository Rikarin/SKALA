// `t?.Item1` is a member binding, not a member access, and names the same field.
public static class Lookup {
    public static int? Find((int Key, string Value)? entry) => entry?.Item1;
}
