using System;

public sealed class Chain {
    public static Action? Without(Action? combined) => combined - (Action)(() => Console.WriteLine("x"));
}
