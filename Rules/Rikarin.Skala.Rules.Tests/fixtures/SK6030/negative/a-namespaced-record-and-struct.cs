namespace Contoso.Model;

public sealed record Order(int Id);

public readonly record struct Money(decimal Amount);

public struct Point {
    public int X;
    public int Y;
}
