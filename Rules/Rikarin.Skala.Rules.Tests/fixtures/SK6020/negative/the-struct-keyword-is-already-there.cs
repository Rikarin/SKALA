using System;

public static class Tight {
    public static string[] All<T>() where T : struct, Enum => Enum.GetNames<T>();
}
