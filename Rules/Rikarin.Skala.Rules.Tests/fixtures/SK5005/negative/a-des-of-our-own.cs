// A user type that happens to be called DES is not the cipher, and the rule matches the type the
// expression produces rather than the identifier that was written.
public sealed class DES {
    public static DES Create() => new();
}

public static class Use {
    public static DES Make() => DES.Create();
}
