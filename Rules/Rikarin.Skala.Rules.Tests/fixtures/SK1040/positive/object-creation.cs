using System;

public sealed class Boxed {
    public static object Wrap(int value) => new Nullable<int>(value);
}
