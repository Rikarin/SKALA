// ⚠ #302's shape (#325). The guard asked over the conditional expression's FULL span, which begins
// after the `=>`, so the comment declined the finding. ⚠ This site also carried the SAME question
// twice — the node walk and a text scan over `FullSpan` on the next line — which was one guard
// pretending to be two; both are now the single span question the fix actually needs.
public sealed class Element {
    public Element? Parent;
}

public sealed class Document {
    public Element? Root;
}

public sealed class Reader {
    public Element? RootOf(Document? document) =>
        // a missing document has no root, and that is not an error worth raising
        document != null ? document.Root : null;
}
