namespace Xunit {
    public sealed class TheoryAttribute : System.Attribute {
        public string? Skip { get; set; }
    }
}

[Xunit.Theory(Skip = "TODO")]
public sealed class Work { }
