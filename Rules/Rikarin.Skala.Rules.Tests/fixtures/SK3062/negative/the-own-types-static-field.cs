// ⚠ `SK2134` (`instance-write-to-static`) reports exactly this line and calls it the canonical shape
// of its own concept. Shape A therefore stops at the constructor's own containing type, so that the
// two rules never both land here: a reader given two findings on one line, in two vocabularies,
// acts on neither. Deleting the containing-type comparison in `StoredInStaticState` turns this
// fixture red, which is the only thing keeping the exclusion honest.
public sealed class Session {
    static Session? current;

    public Session(string user) {
        current = this;
        User = user;
    }

    public static Session? Current => current;

    public string User { get; }
}
