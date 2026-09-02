class Document {
    public int Status { get; set; }
}

class TwoAlternatives {
    public bool Editable(Document d) => d is { Status: 1 } or { Status: 2 };
}
