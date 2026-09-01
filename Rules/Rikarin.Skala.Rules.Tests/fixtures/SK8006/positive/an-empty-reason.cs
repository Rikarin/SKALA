namespace Xunit {
    public sealed class FactAttribute : System.Attribute {
        public string? Skip { get; set; }
    }
}

[Xunit.Fact(Skip = "")]
public sealed class Work { }
