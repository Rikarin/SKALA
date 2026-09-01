using System;

public static class Constructed {
    public static T Make<T>() where T : Enum, new() => new();
}
