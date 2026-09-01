public sealed class FactAttribute : System.Attribute {
    public string? Skip { get; set; }
}

[Fact(Skip = "")]
public sealed class Work { }
