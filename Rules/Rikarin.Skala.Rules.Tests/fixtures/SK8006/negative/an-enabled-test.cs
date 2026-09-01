namespace Xunit {
    public sealed class FactAttribute : System.Attribute {
        public string? Skip { get; set; }
    }
}

[Xunit.Fact]
public sealed class Work { }
