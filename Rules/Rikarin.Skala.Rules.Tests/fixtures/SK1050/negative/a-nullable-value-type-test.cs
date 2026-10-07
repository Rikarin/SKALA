// ⚠ #424: a nullable value type is not a pattern, so `!(o is int?)` → `o is not int?` is CS8116
// (#412's audit).
public static class Probe {
    static bool NotNullableInt(object o) => !(o is int?);

    public static bool Run() => NotNullableInt(3);
}
