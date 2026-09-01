namespace Contoso.Design;

// How C# spells a discriminated union. Deliberate, and not an unreachable type.
public abstract class Shape {
    private Shape() { }

    public sealed class Circle : Shape {
        public double Radius { get; init; }
    }

    public sealed class Square : Shape {
        public double Side { get; init; }
    }
}
