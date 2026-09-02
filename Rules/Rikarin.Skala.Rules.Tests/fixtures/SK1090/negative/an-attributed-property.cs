public sealed class MarkerAttribute : System.Attribute { }

public sealed class Annotated {
    [Marker]
    public int Maximum { get; } = 1;
}
