class Document {
    public int Status { get; set; }
}

class CommentedPattern {
    public bool Editable(Document d) =>
        d is { Status: 1 } /* draft */ or { Status: 2 };
}
