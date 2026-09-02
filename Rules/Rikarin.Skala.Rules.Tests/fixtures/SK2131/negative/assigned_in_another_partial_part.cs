// ⚠ The declaring type is walked through its symbol rather than through one file, so the part that
// assigns the property is found wherever it is. A generated part is read the same way, which is why
// this rule does not fire on every type whose constructor a source generator writes.
partial class Session {
    public int Id { get; }
}

partial class Session {
    public Session(int id) => Id = id;
}
