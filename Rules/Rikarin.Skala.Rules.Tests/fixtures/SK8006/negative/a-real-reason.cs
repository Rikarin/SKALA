namespace Xunit {
    public sealed class FactAttribute : System.Attribute {
        public string? Skip { get; set; }
    }
}

[Xunit.Fact(Skip = "Blocked by #481 on the CI runner.")]
public sealed class Work { }
