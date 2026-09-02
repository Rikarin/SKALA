public sealed class MarkerAttribute : System.Attribute { }

public sealed class Annotated {
    [Marker]
    private int Total { get; set; }

    public void Set(int value) {
        Total = value;
    }

    public int Value() => Total;
}
