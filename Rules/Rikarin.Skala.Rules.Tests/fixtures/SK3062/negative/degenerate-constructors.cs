using System;

// ⚠ The fixture that exists to prove the analyzer does not throw rather than to prove it is right.
// A crashed analyzer reports `AD0001` and then produces nothing at all, so every negative fixture
// passes and the rule reads as having a spotless false-positive record — which is the failure mode
// hardest to see from a green run. Each shape below is one the walk could have assumed away: a
// struct constructor, a `: this(…)` chain, a `: base(…)` chain, an expression-bodied constructor, a
// class primary constructor (which is not a `ConstructorDeclarationSyntax` at all), and a static
// abstract interface member.
public interface IFactory<T> where T : IFactory<T> {
    static abstract T Create();
}

public readonly struct Point {
    public Point(int x) : this(x, 0) { }

    public Point(int x, int y) {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}

public class Tagged(string tag) {
    public string Tag => tag;
}

public sealed class Derived : Tagged {
    public Derived() : base("derived") { }

    public Derived(string tag) : base(tag) => Console.WriteLine(tag);
}
