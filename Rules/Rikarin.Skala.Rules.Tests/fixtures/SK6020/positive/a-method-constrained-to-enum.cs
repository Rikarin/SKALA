using System;

public static class Names {
    public static string[] All<T>() where T : Enum => Enum.GetNames(typeof(T));
}
