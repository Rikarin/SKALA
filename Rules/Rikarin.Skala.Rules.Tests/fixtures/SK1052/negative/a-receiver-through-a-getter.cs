// ⚠ #423: the receiver `this.Parent.Document` is read twice before the fix and once after, and
// `Parent` is a getter with a body — it runs twice and then once.
public sealed class Document {
    public string? Title;
}

public sealed class Section {
    public Document? Document;
}

public sealed class Page {
    readonly Section section = new();

    public Section Parent => section;

    public string? TitleOf() => this.Parent.Document != null ? this.Parent.Document.Title : null;
}
