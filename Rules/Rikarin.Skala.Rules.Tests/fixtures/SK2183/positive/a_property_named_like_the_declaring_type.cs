// ⚠ #424: `Derived.Hello()` → `Base.Hello()` looks the qualifier up as an expression, and the
// property `Base` is nearer than the type. #412's audit measured "Base.Hello (static)" becoming
// "Other.Hello (instance)". The global::-qualified spelling still reaches the static, so the fix
// writes that rather than declining.
public class Base {
    public static string Hello() => "Base.Hello (static)";
}

public class Derived : Base { }

public class Other {
    public string Hello() => "Other.Hello (instance)";
}

public static class Probe {
    static Other Base => new Other();

    public static string Run() => Derived.Hello();
}
