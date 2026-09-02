public sealed class MarkerAttribute : System.Attribute { }

public sealed class Person {
    [Marker]
    public string Name { get; set; } = "x";
}
