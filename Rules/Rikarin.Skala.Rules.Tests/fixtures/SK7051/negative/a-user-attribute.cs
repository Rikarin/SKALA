public sealed class SuppressMessageAttribute : System.Attribute {
    public SuppressMessageAttribute(string category, string id) { }
    public string? Justification { get; set; }
}

[SuppressMessage("Design", "CA1024")]
public sealed class Work { }
