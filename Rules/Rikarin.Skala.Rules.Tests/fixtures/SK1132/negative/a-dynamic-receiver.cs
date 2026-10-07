// A `dynamic` receiver binds nothing at compile time; there is no type to read a name from.
public static class Late {
    public static object First(dynamic value) => value.Item1;
}
