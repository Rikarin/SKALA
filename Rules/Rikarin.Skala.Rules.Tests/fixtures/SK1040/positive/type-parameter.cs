using System;

public sealed class Boxes {
    public static Nullable<T> Wrap<T>(T value) where T : struct => value;
}
