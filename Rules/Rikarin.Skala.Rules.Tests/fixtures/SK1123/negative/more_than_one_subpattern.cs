class Document {
    public int Status { get; set; }

    public int Version { get; set; }
}

class MoreThanOne {
    public bool Editable(Document d) => d is { Status: 1, Version: 3 } or { Status: 2 };
}
