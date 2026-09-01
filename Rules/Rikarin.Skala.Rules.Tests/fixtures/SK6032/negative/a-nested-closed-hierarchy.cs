namespace Contoso.Design;

// ⚠ Found by the sweep rather than by reasoning. How C# spells a closed hierarchy: no `abstract`, no
// `virtual` and nothing `protected`, because the derived types are right there in the body — the
// derivation surface stated as directly as it can be.
public abstract class Shape {
    private Shape() { }

    public sealed class Circle : Shape {
        public double Radius { get; init; }
    }

    public sealed class Square : Shape {
        public double Side { get; init; }
    }
}
