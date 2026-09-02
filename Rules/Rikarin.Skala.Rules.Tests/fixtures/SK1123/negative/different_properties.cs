class Document {
    public int Status { get; set; }

    public int Version { get; set; }
}

class DifferentProperties {
    public bool Interesting(Document d) => d is { Status: 1 } or { Version: 2 };
}
