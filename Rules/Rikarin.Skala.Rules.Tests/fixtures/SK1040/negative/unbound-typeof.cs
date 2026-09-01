using System;

// `typeof(Nullable<>)` names the unbound generic type. `int?` is a constructed type and there is
// no short form of the open one, so there is nothing to rewrite.
public sealed class Reflection {
    public static Type OpenNullable() => typeof(Nullable<>);
}
