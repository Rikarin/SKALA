namespace Contoso.Design;

// The type creates itself, so the private constructor has a caller and the type has an instance.
public sealed class Clock {
    public static readonly Clock Instance = new();

    private Clock() { }

    public int Ticks => 0;
}
